namespace AppTemplate.UseCases.Concurrency;

/// <summary>
/// A client's condition on the version it read (HTTP <c>If-Match</c>): the write happens only if
/// the resource still has one of <see cref="Versions"/>, or exists at all for <see cref="AnyVersion"/>.
/// </summary>
public sealed record VersionPrecondition(bool AnyVersion, IReadOnlyList<uint> Versions)
{
    public static VersionPrecondition Any { get; } = new(true, []);

    public bool IsMetBy(uint version) => AnyVersion || Versions.Contains(version);
}

/// <summary>
/// Results for concurrency outcomes. A failed precondition is a <c>Conflict</c> carrying
/// <see cref="PreconditionFailed"/>, which the HTTP layer maps to 412 rather than 409.
/// </summary>
public static class ConcurrencyResults
{
    public const string PreconditionFailed =
        "The resource has changed since the version in If-Match. Read it again and retry.";

    public const string ConcurrentChange =
        "The resource was changed by someone else while you were saving. Read it again and retry.";
}
