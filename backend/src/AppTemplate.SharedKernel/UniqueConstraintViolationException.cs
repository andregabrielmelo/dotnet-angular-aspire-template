namespace AppTemplate.SharedKernel;

/// <summary>
/// A save broke a unique constraint - typically two concurrent requests inserting the same
/// thing. Thrown by <see cref="IRepository{T}"/> implementations in place of the database
/// provider's own exception, so use cases can handle the race without depending on EF Core or
/// Npgsql. The rejected changes are no longer tracked, so the repository stays usable.
/// </summary>
public sealed class UniqueConstraintViolationException(
    string? constraintName,
    Exception innerException
)
    : Exception(
        $"A unique constraint was violated{(constraintName is null ? "" : $" ({constraintName})")}.",
        innerException
    )
{
    /// <summary>The violated constraint or index, when the database reports it.</summary>
    public string? ConstraintName { get; } = constraintName;
}
