namespace AppTemplate.UseCases.Caching;

/// <summary>
/// Tags group cache entries so writes can invalidate everything derived from the changed
/// data in one call, across both HybridCache and the HTTP output cache.
/// </summary>
public static class CacheTags
{
    /// <summary>
    /// Every cached user read (single users, the current user, user lists). User writes are
    /// rare compared to reads, so a write invalidates this whole tag: simple and always correct.
    /// </summary>
    public const string Users = "users";
}
