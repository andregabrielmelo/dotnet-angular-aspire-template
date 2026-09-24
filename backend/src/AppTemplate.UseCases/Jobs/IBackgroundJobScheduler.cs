using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Jobs;

/// <summary>
/// Schedules work to run outside the request (implemented with Hangfire in Infrastructure).
/// One method per job keeps arguments small and primitive (ids, not objects), which is what
/// ends up serialized in job storage, and keeps use cases free of Hangfire types.
/// </summary>
public interface IBackgroundJobScheduler
{
    /// <returns>The job id.</returns>
    string EnqueueWelcomeEmail(UserId userId);
}
