namespace AppTemplate.Infrastructure.Jobs;

/// <summary>
/// A recurring background job. Implementations are registered in DI (see
/// <c>AddJobScheduling</c>) and scheduled at startup by <c>UseJobSchedulingAsync</c>; every
/// run goes through <see cref="RecurringJobRunner"/>, so Hangfire only ever stores the
/// <see cref="JobId"/>.
/// <para>
/// To add one:
/// <list type="number">
/// <item>Create a class implementing this interface (usually in <c>Jobs/RecurringJobs</c>).</item>
/// <item>Register it in <c>AddJobScheduling</c> with <c>AddRecurringJob&lt;T&gt;()</c>.</item>
/// </list>
/// </para>
/// <para>
/// Hangfire runs jobs at least once, so <see cref="ExecuteAsync"/> must be safe to repeat, and
/// it runs outside any HTTP request: request-scoped services (such as ICurrentUser) are not
/// available.
/// </para>
/// </summary>
public interface IRecurringJobDefinition
{
    /// <summary>
    /// Stable, unique id (e.g. "sync-user-profiles"): lowercase letters, digits and dashes.
    /// It is the key in Hangfire, the dashboard and the admin API - never rename a deployed one.
    /// </summary>
    string JobId { get; }

    /// <summary>Schedule as a cron expression, in UTC. Prefer Hangfire's <c>Cron</c> helpers.</summary>
    string CronExpression { get; }

    /// <param name="cancellationToken">Signalled by Hangfire on server shutdown.</param>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
