using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AppTemplate.UseCases.Telemetry;

/// <summary>
/// Business metrics, exported over OpenTelemetry with the rest (ServiceDefaults adds every
/// "AppTemplate.*" meter): what the application did, as opposed to how the runtime behaved.
/// Each counter is incremented where the thing has actually happened, never where it was only
/// requested. Tags must stay bounded: never a user id, email or other per-user value, because
/// every distinct tag value is a new time series.
/// </summary>
public sealed class ApplicationMetrics
{
    public const string MeterName = "AppTemplate.Application";

    public const string JobTag = "job";
    public const string OutcomeTag = "outcome";

    private readonly Counter<long> _usersProvisioned;
    private readonly Counter<long> _welcomeEmailsSent;
    private readonly Counter<long> _jobRuns;
    private readonly Histogram<double> _jobDuration;

    public ApplicationMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _usersProvisioned = meter.CreateCounter<long>(
            "users.provisioned",
            unit: "{user}",
            description: "Users created on their first sign-in."
        );
        _welcomeEmailsSent = meter.CreateCounter<long>(
            "welcome_emails.sent",
            unit: "{email}",
            description: "Welcome emails the mail server accepted."
        );
        _jobRuns = meter.CreateCounter<long>(
            "jobs.runs",
            unit: "{run}",
            description: "Background job runs, by job and outcome."
        );
        _jobDuration = meter.CreateHistogram<double>(
            "jobs.duration",
            unit: "s",
            description: "How long background job runs took, by job and outcome."
        );
    }

    public void UserProvisioned() => _usersProvisioned.Add(1);

    public void WelcomeEmailSent() => _welcomeEmailsSent.Add(1);

    /// <param name="job">A job id from code (a recurring job id, or a fire-and-forget job name), never input.</param>
    public void JobRun(string job, JobOutcome outcome, TimeSpan duration)
    {
        var tags = new TagList
        {
            { JobTag, job },
            { OutcomeTag, outcome.ToString().ToLowerInvariant() },
        };
        _jobRuns.Add(1, tags);
        _jobDuration.Record(duration.TotalSeconds, tags);
    }
}

public enum JobOutcome
{
    Succeeded,
    Failed,

    /// <summary>Nothing to do, such as a welcome email for a user deleted before the job ran.</summary>
    Skipped,
}
