namespace AppTemplate.Infrastructure.Jobs;

/// <summary>
/// Hangfire queues, listed in priority order for the server. Hangfire requires queue names
/// to be lowercase letters, digits, underscores and dashes.
/// </summary>
public static class JobQueues
{
    /// <summary>User-facing side effects (emails): processed first.</summary>
    public const string Emails = "emails";

    /// <summary>Maintenance and everything else.</summary>
    public const string Default = "default";

    public static readonly string[] All = [Emails, Default];
}
