using System.Collections.Concurrent;
using System.Net.Http.Json;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Outbox;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Telemetry;
using AppTemplate.Web.Features.UserFeatures;
using Mediator;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AppTemplate.FunctionalTests.Outbox;

/// <summary>
/// The outbox's guarantees, against real Postgres (ADR 016): atomic with the change, recovered
/// by the sweep, lease-scoped exclusive claims, back-off then dead-lettering, unknown types
/// never deserialized, and a per-handler inbox that makes redelivery safe.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class OutboxTests(OutboxTests.OutboxFactory factory)
    : IClassFixture<OutboxTests.OutboxFactory>
{
    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private OutboxProcessor Processor => factory.Services.GetRequiredService<OutboxProcessor>();

    /// <summary>Writes an event straight into the outbox, as the interceptor would.</summary>
    private Task<Guid> WriteAsync(IIntegrationEvent integrationEvent) =>
        InScopeAsync(async services =>
        {
            var (key, payload) = services
                .GetRequiredService<IntegrationEventRegistry>()
                .Serialize(integrationEvent);
            return await WriteRawAsync(services, key, payload);
        });

    private static async Task<Guid> WriteRawAsync(
        IServiceProvider services,
        string key,
        string payload
    )
    {
        var context = services.GetRequiredService<ApplicationDatabaseContext>();
        var message = new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Type = key,
            Payload = payload,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            NextAttemptAtUtc = DateTimeOffset.UtcNow,
        };
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();
        return message.Id;
    }

    private Task<OutboxMessage> LoadAsync(Guid id) =>
        InScopeAsync(services =>
            services
                .GetRequiredService<ApplicationDatabaseContext>()
                .OutboxMessages.AsNoTracking()
                .SingleAsync(message => message.Id == id)
        );

    private Task ExecuteSqlAsync(FormattableString sql) =>
        InScopeAsync(services =>
            services.GetRequiredService<ApplicationDatabaseContext>().Database.ExecuteSqlAsync(sql)
        );

    /// <summary>Messages whose payload mentions <paramref name="text"/> (payloads are small; filtered here).</summary>
    private Task<List<OutboxMessage>> MessagesMentioningAsync(string text) =>
        InScopeAsync(async services =>
            (
                await services
                    .GetRequiredService<ApplicationDatabaseContext>()
                    .OutboxMessages.AsNoTracking()
                    .ToListAsync()
            )
                .Where(message => message.Payload.Contains(text, StringComparison.Ordinal))
                .ToList()
        );

    [Fact]
    public async Task ProvisioningAUser_WritesItsOutboxMessageInTheSameCommit()
    {
        var subject = $"sub-{Guid.NewGuid():N}";

        await factory.CreateAuthenticatedClient(subject).GetAsync("/v1/users/me");

        var message = Assert.Single(await MessagesMentioningAsync(subject));
        Assert.Equal("user.provisioned.v1", message.Type);
        Assert.Null(message.ProcessedAtUtc);
    }

    [Fact]
    public async Task RolledBackChange_LeavesNeitherTheRowNorItsOutboxMessage()
    {
        var externalId = $"sub-{Guid.NewGuid():N}";

        await InScopeAsync(async services =>
        {
            var context = services.GetRequiredService<ApplicationDatabaseContext>();
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.Users.Add(
                User.Create(
                    externalId,
                    UserName.From("Ada"),
                    new EmailAddress($"{externalId}@example.com")
                )
            );
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
            return 0;
        });

        Assert.Empty(await MessagesMentioningAsync(externalId));
        Assert.False(
            await InScopeAsync(services =>
                services
                    .GetRequiredService<ApplicationDatabaseContext>()
                    .Users.AnyAsync(user => user.ExternalId == externalId)
            )
        );
    }

    [Fact]
    public async Task CrashBeforeTheFastPath_IsDeliveredByTheSweep()
    {
        // The recording trigger stands in for Hangfire and never runs the relay, exactly as if
        // the process had stopped right after the commit.
        var subject = $"sub-{Guid.NewGuid():N}";
        var triggered = factory.Trigger.Count;
        var me = await factory
            .CreateAuthenticatedClient(subject)
            .GetFromJsonAsync<CurrentUserResponse>("/v1/users/me");
        Assert.True(factory.Trigger.Count > triggered);
        Assert.DoesNotContain(factory.EmailSender.Sent, email => email.To == me!.Email);

        await InScopeAsync(async services =>
        {
            await services
                .GetRequiredService<OutboxSweepJob>()
                .ExecuteAsync(CancellationToken.None);
            return 0;
        });

        Assert.Single(factory.EmailSender.Sent, email => email.To == me!.Email);
        Assert.NotNull(Assert.Single(await MessagesMentioningAsync(subject)).ProcessedAtUtc);
    }

    [Fact]
    public async Task ConcurrentWorkers_NeverClaimTheSameMessage()
    {
        var ids = new HashSet<Guid>();
        for (var i = 0; i < 30; i++)
        {
            ids.Add(await WriteAsync(new TestPing($"claim-{Guid.NewGuid():N}")));
        }

        var claims = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => Processor.ClaimBatchAsync(CancellationToken.None))
        );
        var mine = claims.Select(claim => claim.Where(ids.Contains).ToList()).ToList();

        // Every message claimed exactly once across the four workers...
        Assert.Equal(ids.Count, mine.Sum(claim => claim.Count));
        Assert.Equal(ids.Count, mine.SelectMany(claim => claim).Distinct().Count());
        // ...and while the leases hold, nobody gets them again.
        Assert.DoesNotContain(
            await Processor.ClaimBatchAsync(CancellationToken.None),
            ids.Contains
        );
    }

    [Fact]
    public async Task ExpiredLease_IsClaimableAgain()
    {
        var id = await WriteAsync(new TestPing($"lease-{Guid.NewGuid():N}"));
        Assert.Contains(id, await Processor.ClaimBatchAsync(CancellationToken.None));
        Assert.DoesNotContain(id, await Processor.ClaimBatchAsync(CancellationToken.None));

        // The worker holding it died: its lease runs out.
        await ExecuteSqlAsync(
            $"UPDATE outbox_messages SET locked_until_utc = now() - interval '1 second' WHERE id = {id}"
        );

        Assert.Contains(id, await Processor.ClaimBatchAsync(CancellationToken.None));
    }

    [Fact]
    public async Task FailingHandler_BacksOffThenIsDeadLettered()
    {
        var value = $"fail-{Guid.NewGuid():N}";
        factory.Probe.AlwaysFail.Add(value);
        var id = await WriteAsync(new TestPing(value));
        using var deadLettered = factory.CollectMetric<long>("outbox.messages.dead_lettered");

        Assert.False(await Processor.DeliverAsync(id, CancellationToken.None));
        var afterFirst = await LoadAsync(id);
        Assert.Equal(1, afterFirst.Attempts);
        Assert.Contains("ping failed", afterFirst.LastError);
        Assert.InRange(
            afterFirst.NextAttemptAtUtc - DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(11)
        );
        Assert.Equal(TimeSpan.FromSeconds(20), Processor.RetryDelay(2)); // doubles

        for (var attempt = 2; attempt <= OutboxFactory.MaxAttempts; attempt++)
        {
            Assert.False(await Processor.DeliverAsync(id, CancellationToken.None));
        }

        var dead = await LoadAsync(id);
        Assert.Equal(OutboxFactory.MaxAttempts, dead.Attempts);
        Assert.NotNull(dead.DeadLetteredAtUtc);
        Assert.Null(dead.ProcessedAtUtc);
        Assert.Single(deadLettered.GetMeasurementSnapshot());
        // Dead letters are never claimed again until requeued.
        await ExecuteSqlAsync(
            $"UPDATE outbox_messages SET next_attempt_at_utc = now() WHERE id = {id}"
        );
        Assert.DoesNotContain(id, await Processor.ClaimBatchAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UnknownMessageType_IsDeadLetteredWithoutReadingThePayload()
    {
        var id = await InScopeAsync(services =>
            WriteRawAsync(services, "removed.event.v7", "{\"not\": \"this version's contract\"}")
        );

        Assert.False(await Processor.DeliverAsync(id, CancellationToken.None));

        var message = await LoadAsync(id);
        Assert.NotNull(message.DeadLetteredAtUtc);
        Assert.Equal(0, message.Attempts);
        Assert.Contains("Unknown message type 'removed.event.v7'", message.LastError);
    }

    [Fact]
    public async Task Redelivery_SkipsHandlersThatAlreadyCompleted()
    {
        var value = $"inbox-{Guid.NewGuid():N}";
        factory.Probe.FailSecondHandlerOnce.Add(value);
        var id = await WriteAsync(new TestPing(value));

        Assert.False(await Processor.DeliverAsync(id, CancellationToken.None)); // second handler fails
        Assert.True(await Processor.DeliverAsync(id, CancellationToken.None)); // retry

        Assert.Equal(1, factory.Probe.Completions(nameof(FirstPingHandler), value));
        Assert.Equal(1, factory.Probe.Completions(nameof(SecondPingHandler), value));

        // Delivered again anyway (a worker that outlived its lease): every handler is skipped.
        await ExecuteSqlAsync(
            $"UPDATE outbox_messages SET processed_at_utc = NULL WHERE id = {id}"
        );
        Assert.True(await Processor.DeliverAsync(id, CancellationToken.None));
        Assert.Equal(1, factory.Probe.Completions(nameof(FirstPingHandler), value));
        Assert.Equal(1, factory.Probe.Completions(nameof(SecondPingHandler), value));
    }

    [Fact]
    public async Task RolledBackHandler_LeavesNoInboxRowOrChanges_SoTheRetryRunsIt()
    {
        // The first handler saves a user, then throws: its transaction rolls back both the
        // user and the inbox row, so the retry runs it again and the user exists once.
        var value = $"rollback-{Guid.NewGuid():N}";
        factory.Probe.FailFirstHandlerAfterSavingOnce.Add(value);
        var id = await WriteAsync(new TestPing(value));

        Assert.False(await Processor.DeliverAsync(id, CancellationToken.None));
        Assert.False(
            await InScopeAsync(services =>
                services
                    .GetRequiredService<ApplicationDatabaseContext>()
                    .InboxMessages.AnyAsync(inbox => inbox.MessageId == id)
            )
        );
        Assert.Equal(0, await UsersWithExternalIdAsync(value));

        Assert.True(await Processor.DeliverAsync(id, CancellationToken.None));
        Assert.Equal(1, await UsersWithExternalIdAsync(value));
    }

    [Fact]
    public async Task RedeliveredWelcomeEmail_IsNotSentTwice()
    {
        // The worst case for an external side effect: the email went out, then the process died
        // before the inbox row committed. The redelivery runs the handler again, and the
        // WelcomeEmailSentAtUtc guard stops a second email.
        var subject = $"sub-{Guid.NewGuid():N}";
        var me = await factory
            .CreateAuthenticatedClient(subject)
            .GetFromJsonAsync<CurrentUserResponse>("/v1/users/me");
        var id = Assert.Single(await MessagesMentioningAsync(subject)).Id;
        Assert.True(await Processor.DeliverAsync(id, CancellationToken.None));

        await ExecuteSqlAsync($"DELETE FROM inbox_messages WHERE message_id = {id}");
        await ExecuteSqlAsync(
            $"UPDATE outbox_messages SET processed_at_utc = NULL WHERE id = {id}"
        );
        Assert.True(await Processor.DeliverAsync(id, CancellationToken.None));

        Assert.Single(factory.EmailSender.Sent, email => email.To == me!.Email);
    }

    [Fact]
    public async Task RequeueEndpoint_MakesDeadLettersDueAgain()
    {
        var value = $"requeue-{Guid.NewGuid():N}";
        factory.Probe.AlwaysFail.Add(value);
        var id = await WriteAsync(new TestPing(value));
        for (var attempt = 1; attempt <= OutboxFactory.MaxAttempts; attempt++)
        {
            await Processor.DeliverAsync(id, CancellationToken.None);
        }
        Assert.NotNull((await LoadAsync(id)).DeadLetteredAtUtc);
        factory.Probe.AlwaysFail.Remove(value); // the bug is fixed

        var response = await factory
            .CreateAuthenticatedClient(
                $"sub-{Guid.NewGuid():N}",
                UseCases.Authorization.Permission.JobsManage
            )
            .PostAsync("/v1/admin/outbox/dead-letters/requeue", null);

        response.EnsureSuccessStatusCode();
        var requeued = await LoadAsync(id);
        Assert.Null(requeued.DeadLetteredAtUtc);
        Assert.Equal(0, requeued.Attempts);
        Assert.True(await Processor.DeliverAsync(id, CancellationToken.None));
    }

    private Task<int> UsersWithExternalIdAsync(string externalId) =>
        InScopeAsync(services =>
            services
                .GetRequiredService<ApplicationDatabaseContext>()
                .Users.CountAsync(user => user.ExternalId == externalId)
        );

    [IntegrationEvent("test.ping", 1)]
    public sealed record TestPing(string Value) : IIntegrationEvent;

    /// <summary>Controls and observes the test handlers, by event value.</summary>
    public sealed class PingProbe
    {
        private readonly ConcurrentDictionary<(string Handler, string Value), int> _completions =
            new();

        public HashSet<string> AlwaysFail { get; } = [];

        public HashSet<string> FailSecondHandlerOnce { get; } = [];

        public HashSet<string> FailFirstHandlerAfterSavingOnce { get; } = [];

        public HashSet<string> FailedAfterSaving { get; } = [];

        public int Completions(string handler, string value) =>
            _completions.GetValueOrDefault((handler, value));

        public void Completed(string handler, string value) =>
            _completions.AddOrUpdate((handler, value), 1, (_, count) => count + 1);
    }

    public sealed class FirstPingHandler(PingProbe probe, ApplicationDatabaseContext context)
        : INotificationHandler<TestPing>
    {
        public async ValueTask Handle(TestPing ping, CancellationToken cancellationToken)
        {
            if (probe.AlwaysFail.Contains(ping.Value))
            {
                throw new InvalidOperationException("ping failed");
            }
            if (probe.FailFirstHandlerAfterSavingOnce.Contains(ping.Value))
            {
                context.Users.Add(
                    User.Create(
                        ping.Value,
                        UserName.From("Ping"),
                        new EmailAddress($"{ping.Value}@example.com")
                    )
                );
                await context.SaveChangesAsync(cancellationToken);
                if (probe.FailedAfterSaving.Add(ping.Value))
                {
                    throw new InvalidOperationException("ping failed after saving");
                }
            }
            probe.Completed(nameof(FirstPingHandler), ping.Value);
        }
    }

    public sealed class SecondPingHandler(PingProbe probe) : INotificationHandler<TestPing>
    {
        public ValueTask Handle(TestPing ping, CancellationToken cancellationToken)
        {
            if (probe.FailSecondHandlerOnce.Remove(ping.Value))
            {
                throw new InvalidOperationException("second ping handler failed");
            }
            probe.Completed(nameof(SecondPingHandler), ping.Value);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class RecordingTrigger : IOutboxTrigger
    {
        private int _count;

        public int Count => _count;

        public void MessagesWritten() => Interlocked.Increment(ref _count);
    }

    public sealed class OutboxFactory : AppTemplateWebApplicationFactory
    {
        public const int MaxAttempts = 3;

        public PingProbe Probe { get; } = new();

        public RecordingTrigger Trigger { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Outbox:MaxAttempts", MaxAttempts.ToString());
            builder.ConfigureServices(services =>
            {
                services.Configure<OutboxOptions>(options => options.AddEvent<TestPing>());
                services.AddSingleton(Probe);
                services.AddScoped<INotificationHandler<TestPing>, FirstPingHandler>();
                services.AddScoped<INotificationHandler<TestPing>, SecondPingHandler>();
                services.RemoveAll<IOutboxTrigger>();
                services.AddSingleton<IOutboxTrigger>(Trigger);
            });
        }
    }
}
