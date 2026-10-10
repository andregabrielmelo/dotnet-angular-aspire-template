using AppTemplate.Core.Jobs;

namespace AppTemplate.Core.Interfaces;

/// <summary>
/// Inspects and manages recurring background jobs - the production alternative to the
/// (development-only) Hangfire dashboard, exposed through the permission-gated admin API.
/// Implemented in Infrastructure on top of Hangfire. Job definitions themselves are registered
/// in code and scheduled at startup; this service changes their runtime state.
/// <para>
/// Endpoints reach it through Mediator, like the rest of the API: the queries in
/// <c>Jobs/Get</c> and <c>Jobs/List</c>, and one command per action (<c>Jobs/Trigger</c>,
/// <c>Pause</c>, <c>Resume</c>, <c>Remove</c>, <c>Restore</c>).
/// </para>
/// Implemented by <c>JobManagementService</c>.
/// </summary>
public interface IJobManagementService
{
    Task<IReadOnlyList<RecurringJobDto>> GetRecurringJobsAsync(CancellationToken cancellationToken);

    /// <returns>The job with its recent runs, or <see cref="ResultStatus.NotFound"/>.</returns>
    Task<Result<RecurringJobDetailDto>> GetRecurringJobAsync(
        string jobId,
        CancellationToken cancellationToken
    );

    /// <summary>Runs the job now, without changing its schedule. Works while paused too.</summary>
    Task<Result> TriggerAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>Skips scheduled runs until resumed; survives restarts. Pausing twice is a no-op.</summary>
    Task<Result> PauseAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>Scheduled runs happen again. Resuming a job that isn't paused is a no-op.</summary>
    Task<Result> ResumeAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the job from the scheduler (and forgets its pause). It comes back on the next
    /// startup or <see cref="RestoreAsync"/>, because definitions live in code.
    /// </summary>
    Task<Result> RemoveAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>
    /// Syncs the scheduler with the job definitions in code: re-registers every definition
    /// (keeping paused jobs paused) and removes recurring jobs whose definition no longer exists.
    /// </summary>
    Task<Result> RestoreAsync(CancellationToken cancellationToken);
}
