namespace AppTemplate.Core.Aggregates.UserAggregate.Specifications;

/// <summary>
/// Read-only lookup (used by GetUserHandler) - AsNoTracking since nothing here mutates the
/// result. Writes go through IRepository.GetByIdAsync directly, not this specification.
/// </summary>
public class UserByIdSpecification : Specification<User>
{
    public UserByIdSpecification(UserId personId) =>
        Query.Where(person => person.Id == personId).AsNoTracking();
}
