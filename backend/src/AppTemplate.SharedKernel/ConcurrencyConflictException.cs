namespace AppTemplate.SharedKernel;

/// <summary>
/// Saving failed because the row changed after it was loaded (optimistic concurrency). Thrown
/// by the repository instead of EF Core's own exception, so use cases can handle it without
/// referencing EF Core. Handlers that care turn it into 409 or 412; anywhere else it surfaces
/// as a 409 problem details response.
/// </summary>
public sealed class ConcurrencyConflictException(Exception inner)
    : Exception("The data changed after it was read. Reload it and try again.", inner);
