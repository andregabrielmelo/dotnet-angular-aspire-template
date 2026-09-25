using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Infrastructure.Jobs.FireAndForget;
using AppTemplate.UseCases.Jobs;
using Hangfire;

namespace AppTemplate.Infrastructure.Jobs.Services;

public sealed class HangfireBackgroundJobScheduler(IBackgroundJobClient client)
    : IBackgroundJobScheduler
{
    // CancellationToken.None is a placeholder: Hangfire substitutes its own shutdown-aware
    // token when the job runs.
    public string EnqueueWelcomeEmail(UserId userId) =>
        client.Enqueue<WelcomeEmailJob>(
            JobQueues.Emails,
            job => job.ExecuteAsync(userId.Value, CancellationToken.None)
        );
}
