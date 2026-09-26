using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Jobs.Options;

/// <summary>Bound from the <c>JobScheduling</c> configuration section.</summary>
public sealed class JobSchedulingOptions
{
    public const string SectionName = "JobScheduling";

    /// <summary>
    /// When false, Hangfire isn't registered at all: nothing is enqueued or scheduled, and the
    /// rest of the application keeps working (background work is skipped with a warning).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether this process also executes jobs. Every instance can enqueue; turn this off to
    /// process jobs in a separate worker (or, in tests, not at all). Recurring jobs are only
    /// scheduled while at least one server runs, so keep one running somewhere.
    /// </summary>
    public bool RunServer { get; set; } = true;

    [Range(1, 1000)]
    public int WorkerCount { get; set; } = Environment.ProcessorCount;
}

[OptionsValidator]
public sealed partial class JobSchedulingOptionsValidator : IValidateOptions<JobSchedulingOptions>;
