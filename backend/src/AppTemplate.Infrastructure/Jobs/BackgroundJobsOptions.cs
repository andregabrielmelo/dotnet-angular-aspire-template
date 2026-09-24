using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Jobs;

/// <summary>Bound from the <c>BackgroundJobs</c> configuration section.</summary>
public sealed class BackgroundJobsOptions
{
    public const string SectionName = "BackgroundJobs";

    /// <summary>
    /// Whether this process also executes jobs. Every instance can enqueue; turn this off to
    /// run job processing in a separate process (or, in tests, not at all).
    /// </summary>
    public bool RunServer { get; set; } = true;

    [Range(1, 100)]
    public int WorkerCount { get; set; } = Math.Min(Environment.ProcessorCount * 2, 10);

    /// <summary>Postgres schema for Hangfire's tables, kept apart from the application's.</summary>
    [Required]
    public string Schema { get; set; } = "hangfire";

    /// <summary>Cron schedule (UTC) of the Keycloak profile sync. Default: hourly.</summary>
    [Required]
    public string SyncUserProfilesCron { get; set; } = "0 * * * *";
}

[OptionsValidator]
public sealed partial class BackgroundJobsOptionsValidator
    : IValidateOptions<BackgroundJobsOptions>;
