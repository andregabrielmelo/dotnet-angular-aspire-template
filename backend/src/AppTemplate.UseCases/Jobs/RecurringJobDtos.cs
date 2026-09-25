namespace AppTemplate.UseCases.Jobs;

/// <param name="Id">The stable recurring job id (e.g. "sync-user-profiles").</param>
/// <param name="Cron">The job's own schedule - also while paused, when Hangfire's schedule never fires.</param>
/// <param name="NextExecution">Null while paused.</param>
/// <param name="LastStatus">Hangfire state of the last run, e.g. "Succeeded" or "Failed".</param>
public sealed record RecurringJobDto(
    string Id,
    string Cron,
    DateTimeOffset? NextExecution,
    DateTimeOffset? LastExecution,
    string? LastStatus,
    bool IsPaused,
    DateTimeOffset? CreatedAt
);

/// <param name="JobId">The Hangfire background job id of this run.</param>
/// <param name="Status">"Succeeded" or "Failed".</param>
/// <param name="FinishedAt">When the run succeeded or failed.</param>
/// <param name="Duration">Only known for successful runs.</param>
public sealed record JobExecutionDto(
    string JobId,
    string Status,
    DateTimeOffset? FinishedAt,
    TimeSpan? Duration,
    string? Error
);

public sealed record RecurringJobDetailDto(
    RecurringJobDto Job,
    IReadOnlyList<JobExecutionDto> RecentExecutions
);
