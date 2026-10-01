using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Jobs.Options;

/// <summary>Bound from the <c>JobScheduling</c> configuration section.</summary>
public sealed class JobSchedulingOptions
{
    public const string SectionName = "JobScheduling";

    /// <summary>
    /// Whether this process also executes jobs. Every instance can enqueue; turn this off to
    /// process jobs in a separate worker (or, in tests, not at all). Recurring jobs are only
    /// scheduled while at least one server runs, so keep one running somewhere.
    /// </summary>
    public bool RunServer { get; set; } = true;

    /// <summary>
    /// Whether startup creates and upgrades Hangfire's own tables (in the <c>hangfire</c> schema).
    /// Turn it off where the application's database user may not run DDL, and install the
    /// schema with Hangfire.PostgreSql's scripts instead.
    /// </summary>
    public bool PrepareSchema { get; set; } = true;

    [Range(1, 1000)]
    public int WorkerCount { get; set; } = Environment.ProcessorCount;
}

[OptionsValidator]
public sealed partial class JobSchedulingOptionsValidator : IValidateOptions<JobSchedulingOptions>;
