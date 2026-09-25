namespace AppTemplate.Infrastructure.Jobs.Models;

/// <summary>
/// A paused recurring job. Hangfire has no native pause, so a paused job stays registered with
/// a never-firing schedule and this row remembers the schedule to restore. The database - not
/// process memory - is the only source of truth, so every API instance agrees and the pause
/// survives restarts.
/// </summary>
public class PausedJob
{
    public Guid Id { get; set; }

    /// <summary>The <see cref="IRecurringJobDefinition.JobId"/>.</summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>The cron expression the job had when it was paused.</summary>
    public string OriginalCron { get; set; } = string.Empty;

    public DateTimeOffset PausedAtUtc { get; set; }
}
