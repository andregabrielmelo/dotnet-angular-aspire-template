using Hangfire.Storage;

namespace AppTemplate.Infrastructure.Jobs;

/// <summary>
/// Pause state of recurring jobs, kept as a set in Hangfire's own storage. Hangfire has no
/// native pause, so a paused job keeps its schedule and <see cref="SkipWhenPausedAttribute"/>
/// cancels the runs it would create. Every instance reads the same storage, the pause survives
/// restarts, and adding or removing a set member is idempotent - no races to handle.
/// </summary>
public static class PausedRecurringJobs
{
    public const string SetKey = "apptemplate:paused-recurring-jobs";

    public static bool IsPaused(IStorageConnection connection, string jobId) =>
        connection.GetAllItemsFromSet(SetKey).Contains(jobId);

    public static IReadOnlySet<string> GetAll(IStorageConnection connection) =>
        connection.GetAllItemsFromSet(SetKey);

    public static void Pause(IStorageConnection connection, string jobId)
    {
        using var transaction = connection.CreateWriteTransaction();
        transaction.AddToSet(SetKey, jobId);
        transaction.Commit();
    }

    public static void Resume(IStorageConnection connection, string jobId)
    {
        using var transaction = connection.CreateWriteTransaction();
        transaction.RemoveFromSet(SetKey, jobId);
        transaction.Commit();
    }
}
