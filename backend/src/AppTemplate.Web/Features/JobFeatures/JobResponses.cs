using AppTemplate.UseCases.Jobs;

namespace AppTemplate.Web.Features.JobFeatures;

public sealed record RecurringJobResponse(
    string Id,
    string Cron,
    DateTimeOffset? NextExecution,
    DateTimeOffset? LastExecution,
    string? LastStatus,
    bool IsPaused,
    DateTimeOffset? CreatedAt
)
{
    public static RecurringJobResponse From(RecurringJobDto job) =>
        new(
            job.Id,
            job.Cron,
            job.NextExecution,
            job.LastExecution,
            job.LastStatus,
            job.IsPaused,
            job.CreatedAt
        );
}

public sealed record JobExecutionResponse(
    string JobId,
    string Status,
    DateTimeOffset? FinishedAt,
    double? DurationMs,
    string? Error
);

public sealed record RecurringJobDetailResponse(
    RecurringJobResponse Job,
    IReadOnlyList<JobExecutionResponse> RecentExecutions
)
{
    public static RecurringJobDetailResponse From(RecurringJobDetailDto detail) =>
        new(
            RecurringJobResponse.From(detail.Job),
            detail
                .RecentExecutions.Select(e => new JobExecutionResponse(
                    e.JobId,
                    e.Status,
                    e.FinishedAt,
                    e.Duration?.TotalMilliseconds,
                    e.Error
                ))
                .ToList()
        );
}

/// <summary>Route model shared by every single-job endpoint.</summary>
public sealed class JobIdRequest
{
    public string JobId { get; set; } = string.Empty;
}

public sealed class JobIdValidator : Validator<JobIdRequest>
{
    public JobIdValidator()
    {
        // Same shape as IRecurringJobDefinition.JobId (lowercase, digits, dashes).
        RuleFor(x => x.JobId)
            .NotEmpty()
            .Matches("^[a-z0-9][a-z0-9-]{0,99}$")
            .WithMessage("Job id must be lowercase letters, digits and dashes (max 100).");
    }
}

/// <summary>Shared settings for the job management endpoints.</summary>
internal static class JobEndpoints
{
    public const string Tag = "Jobs";

    /// <summary>Per client IP, like netrock's admin-mutation rate limit.</summary>
    public const int MutationsPerMinute = 30;
}
