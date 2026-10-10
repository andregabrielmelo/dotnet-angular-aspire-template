namespace AppTemplate.UseCases.Auditing.List;

/// <param name="From">Defaults to <see cref="DefaultRange"/> before <paramref name="To"/>.</param>
/// <param name="To">Defaults to now.</param>
public sealed record ListAuditEntriesQuery(
    string? EntityType,
    string? EntityKey,
    string? Actor,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PerPage
) : IQuery<Result<PagedResult<AuditEntryDto>>>
{
    public const int MaxPerPage = 100;

    public static readonly TimeSpan DefaultRange = TimeSpan.FromDays(7);
}
