namespace AppTemplate.Infrastructure.Caching;

/// <summary>
/// Bound from the <c>Cache</c> configuration section. Expirations apply to every entry - see ADR 007.
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>Run HybridCache L1-only (in-memory, per instance) when no Redis is configured.</summary>
    public bool AllowLocalOnly { get; init; }

    /// <summary>Overall lifetime of an entry, including in Redis (L2).</summary>
    public TimeSpan Expiration { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Lifetime in each instance's memory (L1). Local entries are not invalidated across
    /// instances, so keep this short.
    /// </summary>
    public TimeSpan LocalExpiration { get; init; } = TimeSpan.FromMinutes(1);
}
