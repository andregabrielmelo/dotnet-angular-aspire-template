using System.Diagnostics;
using System.Text.Json;
using AppTemplate.Infrastructure.Data;
using AppTemplate.UseCases.Telemetry;
using Mediator;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Outbox;

/// <summary>
/// The relay: claims a batch of pending messages and delivers each to every handler of its
/// event type. Guarantees (see docs/content/reliability-semantics.md):
/// <list type="bullet">
/// <item><b>At-least-once.</b> A message is marked processed only after every handler
/// succeeded; a crash in between means it's delivered again.</item>
/// <item><b>Lease-scoped exclusivity.</b> Claims use <c>FOR UPDATE SKIP LOCKED</c> and a lease
/// (<see cref="OutboxMessage.LockedUntilUtc"/>), so two workers never hold the same message
/// while its lease is valid; an expired lease is claimable again.</item>
/// <item><b>Per-handler inbox.</b> Each handler runs in its own scope and transaction, which
/// also records the <see cref="InboxMessage"/>; a redelivery skips handlers that completed.
/// That makes a handler's <i>database</i> changes effectively-once. External side effects
/// (email) can still repeat if the process dies between the side effect and the commit, so
/// such handlers keep their own guard (like <c>User.WelcomeEmailSentAtUtc</c>).</item>
/// </list>
/// </summary>
public sealed partial class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IntegrationEventRegistry registry,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ApplicationMetrics metrics,
    ILogger<OutboxProcessor> logger
)
{
    private readonly OutboxOptions _options = options.Value;

    /// <summary>Claims and delivers batches until nothing is due. Returns how many it delivered.</summary>
    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var delivered = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await ClaimBatchAsync(cancellationToken);
            foreach (var messageId in batch)
            {
                if (await DeliverAsync(messageId, cancellationToken))
                {
                    delivered++;
                }
            }
            if (batch.Count < _options.BatchSize)
            {
                break;
            }
        }
        return delivered;
    }

    /// <summary>
    /// Takes up to <see cref="OutboxOptions.BatchSize"/> due, unclaimed messages and leases them
    /// to this worker. Rows another transaction is claiming right now are skipped, not waited on.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> ClaimBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDatabaseContext>();
        var now = timeProvider.GetUtcNow();

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        var messages = await context
            .OutboxMessages.FromSql(
                $"""
                SELECT * FROM outbox_messages
                WHERE processed_at_utc IS NULL
                  AND dead_lettered_at_utc IS NULL
                  AND next_attempt_at_utc <= {now}
                  AND (locked_until_utc IS NULL OR locked_until_utc < {now})
                ORDER BY occurred_at_utc
                LIMIT {_options.BatchSize}
                FOR UPDATE SKIP LOCKED
                """
            )
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.LockedUntilUtc = now + _options.LeaseDuration;
        }
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return messages.Select(message => message.Id).ToList();
    }

    /// <summary>Delivers one claimed message; returns whether it's now processed.</summary>
    public async Task<bool> DeliverAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDatabaseContext>();
        var message = await context.OutboxMessages.SingleAsync(
            m => m.Id == messageId,
            cancellationToken
        );
        var stopwatch = Stopwatch.StartNew();

        var type = registry.TypeOf(message.Type);
        if (type is null)
        {
            // Never guess a type for an unknown key: it was produced by a newer or older
            // version that registered a contract this one doesn't know.
            await DeadLetterAsync(context, message, $"Unknown message type '{message.Type}'.");
            // Not the key itself: it's data from storage, and metric tags must stay bounded.
            metrics.OutboxMessageDeadLettered("unknown");
            return false;
        }

        try
        {
            var integrationEvent = registry.Deserialize(message.Payload, type);
            await DispatchToEachHandlerAsync(message.Id, integrationEvent, type, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RecordFailureAsync(context, message, exception);
            return false;
        }

        message.ProcessedAtUtc = timeProvider.GetUtcNow();
        message.LockedUntilUtc = null;
        message.LastError = null;
        await context.SaveChangesAsync(CancellationToken.None);
        metrics.OutboxMessageProcessed(message.Type, stopwatch.Elapsed);
        return true;
    }

    private async Task DispatchToEachHandlerAsync(
        Guid messageId,
        IIntegrationEvent integrationEvent,
        Type eventType,
        CancellationToken cancellationToken
    )
    {
        var handlerInterface = typeof(INotificationHandler<>).MakeGenericType(eventType);
        List<Type> handlerTypes;
        await using (var probe = scopeFactory.CreateAsyncScope())
        {
            handlerTypes = probe
                .ServiceProvider.GetServices(handlerInterface)
                .Select(handler => handler!.GetType())
                .Distinct()
                .ToList();
        }

        foreach (var handlerType in handlerTypes)
        {
            // Its own scope, so the handler's DbContext is the one inside this transaction.
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDatabaseContext>();
            var consumer = handlerType.FullName!;

            await using var transaction = await context.Database.BeginTransactionAsync(
                cancellationToken
            );
            if (
                await context.InboxMessages.AnyAsync(
                    inbox => inbox.MessageId == messageId && inbox.Consumer == consumer,
                    cancellationToken
                )
            )
            {
                continue; // already handled by an earlier delivery
            }

            var handler = scope
                .ServiceProvider.GetServices(handlerInterface)
                .First(candidate => candidate!.GetType() == handlerType)!;
            await InvokeAsync(handler, handlerInterface, integrationEvent, cancellationToken);

            context.InboxMessages.Add(
                new InboxMessage
                {
                    MessageId = messageId,
                    Consumer = consumer,
                    ProcessedAtUtc = timeProvider.GetUtcNow(),
                }
            );
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static ValueTask InvokeAsync(
        object handler,
        Type handlerInterface,
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken
    ) =>
        (ValueTask)
            handlerInterface
                .GetMethod(nameof(INotificationHandler<IIntegrationEvent>.Handle))!
                .Invoke(handler, [integrationEvent, cancellationToken])!;

    private async Task RecordFailureAsync(
        ApplicationDatabaseContext context,
        OutboxMessage message,
        Exception exception
    )
    {
        // Reflection wraps the handler's exception; record the real one.
        var cause = exception
            is System.Reflection.TargetInvocationException { InnerException: { } inner }
            ? inner
            : exception;
        message.Attempts++;
        message.LockedUntilUtc = null;
        message.LastError = Truncate($"{cause.GetType().Name}: {cause.Message}");

        if (message.Attempts >= _options.MaxAttempts || cause is JsonException)
        {
            await DeadLetterAsync(context, message, message.LastError);
            metrics.OutboxMessageDeadLettered(message.Type);
            return;
        }

        message.NextAttemptAtUtc = timeProvider.GetUtcNow() + RetryDelay(message.Attempts);
        await context.SaveChangesAsync(CancellationToken.None);
        metrics.OutboxMessageFailed(message.Type);
        LogDeliveryFailed(logger, cause, message.Id, message.Type, message.Attempts);
    }

    private async Task DeadLetterAsync(
        ApplicationDatabaseContext context,
        OutboxMessage message,
        string reason
    )
    {
        message.DeadLetteredAtUtc = timeProvider.GetUtcNow();
        message.LockedUntilUtc = null;
        message.LastError = Truncate(reason);
        await context.SaveChangesAsync(CancellationToken.None);
        LogDeadLettered(logger, message.Id, message.Type, message.Attempts, reason);
    }

    /// <summary>Exponential back-off: first delay, doubled per attempt, capped.</summary>
    public TimeSpan RetryDelay(int attempts)
    {
        var delay = _options.FirstRetryDelay * Math.Pow(2, Math.Min(attempts - 1, 20));
        return delay < _options.MaxRetryDelay ? delay : _options.MaxRetryDelay;
    }

    private static string Truncate(string value) => value.Length <= 2000 ? value : value[..2000];

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Outbox message {MessageId} ({MessageType}) failed on attempt {Attempts}; it will be retried"
    )]
    private static partial void LogDeliveryFailed(
        ILogger logger,
        Exception exception,
        Guid messageId,
        string messageType,
        int attempts
    );

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Outbox message {MessageId} ({MessageType}) was dead-lettered after {Attempts} attempts: {Reason}"
    )]
    private static partial void LogDeadLettered(
        ILogger logger,
        Guid messageId,
        string messageType,
        int attempts,
        string reason
    );
}
