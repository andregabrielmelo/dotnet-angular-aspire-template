using AppTemplate.Infrastructure.Data;
using AppTemplate.Infrastructure.Jobs;
using AppTemplate.Infrastructure.Jobs.Models;
using AppTemplate.Infrastructure.Jobs.Services;
using Hangfire;
using Hangfire.InMemory;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AppTemplate.FunctionalTests.Jobs;

/// <summary>
/// Two admins acting on the same job at once: the second request loses the race to the
/// database, but the outcome is what it asked for, so it must succeed rather than return 500.
/// The competing request is simulated by a <see cref="SaveChangesInterceptor"/> that writes
/// through a second context right before the service saves.
/// </summary>
public class JobManagementConcurrencyTests
{
    private const string JobId = TestRecurringJobDefinition.Id;

    private readonly string _databaseName = $"JobConcurrency-{Guid.NewGuid()}";
    private readonly InMemoryStorage _storage = new();

    private ApplicationDatabaseContext CreateContext(params IInterceptor[] interceptors) =>
        new(
            new DbContextOptionsBuilder<ApplicationDatabaseContext>()
                .UseInMemoryDatabase(_databaseName)
                .AddInterceptors(interceptors)
                .Options
        );

    private (JobManagementService Service, RecurringJobRegistrar Registrar) CreateService(
        ApplicationDatabaseContext dbContext
    )
    {
        IRecurringJobDefinition[] definitions = [new TestRecurringJobDefinition(new())];
        var jobManager = new RecurringJobManager(_storage);
        var registrar = new RecurringJobRegistrar(
            definitions,
            jobManager,
            _storage,
            dbContext,
            NullLogger<RecurringJobRegistrar>.Instance
        );
        var service = new JobManagementService(
            _storage,
            jobManager,
            registrar,
            definitions,
            dbContext,
            TimeProvider.System,
            NullLogger<JobManagementService>.Instance
        );
        return (service, registrar);
    }

    private async Task InsertPauseRowAsync()
    {
        await using var db = CreateContext();
        db.PausedJobs.Add(
            new PausedJob
            {
                Id = Guid.NewGuid(),
                JobId = JobId,
                OriginalCron = Cron.Daily(),
                PausedAtUtc = DateTimeOffset.UtcNow,
            }
        );
        await db.SaveChangesAsync();
    }

    private async Task DeletePauseRowAsync()
    {
        await using var db = CreateContext();
        db.PausedJobs.RemoveRange(db.PausedJobs.Where(p => p.JobId == JobId));
        await db.SaveChangesAsync();
    }

    private async Task<int> PauseRowCountAsync()
    {
        await using var db = CreateContext();
        return await db.PausedJobs.CountAsync(p => p.JobId == JobId);
    }

    private string? StoredCron()
    {
        using var connection = _storage.GetConnection();
        return connection.GetRecurringJobs().SingleOrDefault(j => j.Id == JobId)?.Cron;
    }

    [Fact]
    public async Task Pause_WhenAnotherRequestPausedFirst_Succeeds()
    {
        // The rival inserts its row; ours is then rejected, as Postgres' unique index would.
        await using var db = CreateContext(
            new CompetingWriteInterceptor(
                InsertPauseRowAsync,
                thenThrow: new DbUpdateException("duplicate key value violates unique constraint")
            )
        );
        var (service, registrar) = CreateService(db);
        registrar.Schedule(JobId, Cron.Daily());

        var result = await service.PauseAsync(JobId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await PauseRowCountAsync());
    }

    [Fact]
    public async Task Pause_WhenTheSaveFailsForAnotherReason_StillFails()
    {
        // No rival row: this is a real database error and must not be swallowed.
        await using var db = CreateContext(
            new CompetingWriteInterceptor(
                () => Task.CompletedTask,
                thenThrow: new DbUpdateException("connection lost")
            )
        );
        var (service, registrar) = CreateService(db);
        registrar.Schedule(JobId, Cron.Daily());

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.PauseAsync(JobId, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Resume_WhenAnotherRequestResumedFirst_SucceedsAndRestoresTheSchedule()
    {
        await InsertPauseRowAsync();
        await using var db = CreateContext(new CompetingWriteInterceptor(DeletePauseRowAsync));
        var (service, registrar) = CreateService(db);
        registrar.Schedule(JobId, Cron.Never());

        var result = await service.ResumeAsync(JobId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, await PauseRowCountAsync());
        Assert.Equal(Cron.Daily(), StoredCron());
    }

    [Fact]
    public async Task Remove_WhenTheRowWasDeletedConcurrently_Succeeds()
    {
        await InsertPauseRowAsync();
        await using var db = CreateContext(new CompetingWriteInterceptor(DeletePauseRowAsync));
        var (service, registrar) = CreateService(db);
        registrar.Schedule(JobId, Cron.Never());

        var result = await service.RemoveAsync(JobId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(StoredCron());
    }

    /// <summary>Runs a competing write before the first save, then optionally fails that save.</summary>
    private sealed class CompetingWriteInterceptor(
        Func<Task> competingWrite,
        Exception? thenThrow = null
    ) : SaveChangesInterceptor
    {
        private bool _raced;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            if (!_raced)
            {
                _raced = true;
                await competingWrite();
                if (thenThrow is not null)
                {
                    throw thenThrow;
                }
            }

            return result;
        }
    }
}
