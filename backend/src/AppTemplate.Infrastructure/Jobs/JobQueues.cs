namespace AppTemplate.Infrastructure.Jobs;

/// <summary>
/// Hangfire queues. Hangfire.PostgreSql fetches from a server's queues in <b>alphabetical</b>
/// order, not in the order they're listed, so a queue's name decides its priority: "critical"
/// sorts before "default". Names must be lowercase letters, digits, underscores and dashes.
/// </summary>
public static class JobQueues
{
    /// <summary>User-facing side effects (emails): processed first.</summary>
    public const string Critical = "critical";

    /// <summary>Maintenance and everything else.</summary>
    public const string Default = "default";

    public static readonly string[] All = [Critical, Default];
}
