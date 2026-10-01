using Hangfire.Client;
using Hangfire.Common;

namespace AppTemplate.Infrastructure.Jobs;

/// <summary>
/// Cancels the creation of a run of a paused recurring job (see
/// <see cref="PausedRecurringJobs"/>), so no run is created, nothing shows up in the history,
/// and Hangfire's scheduler just moves on to the next occurrence. Hangfire marks every run it
/// creates from a recurring job with the <c>RecurringJobId</c> parameter; jobs enqueued directly
/// don't have it and are never cancelled.
/// <para>
/// A method attribute rather than a global filter: Hangfire's global filters are process-wide
/// static state, and this needs nothing from DI - it reads the storage it's handed.
/// </para>
/// </summary>
public sealed class SkipWhenPausedAttribute : JobFilterAttribute, IClientFilter
{
    public const string RecurringJobIdParameter = "RecurringJobId";

    public void OnCreating(CreatingContext context)
    {
        if (
            context.Parameters.TryGetValue(RecurringJobIdParameter, out var value)
            && value is string jobId
            && PausedRecurringJobs.IsPaused(context.Connection, jobId)
        )
        {
            context.Canceled = true;
        }
    }

    public void OnCreated(CreatedContext context) { }
}
