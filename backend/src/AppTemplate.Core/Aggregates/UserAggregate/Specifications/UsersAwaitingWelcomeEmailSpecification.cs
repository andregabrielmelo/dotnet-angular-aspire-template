namespace AppTemplate.Core.Aggregates.UserAggregate.Specifications;

/// <summary>
/// Ids of users created inside a time window who haven't had their welcome email yet, oldest
/// first (read-only).
/// </summary>
public class UsersAwaitingWelcomeEmailSpecification : Specification<User, UserId>
{
    public UsersAwaitingWelcomeEmailSpecification(
        DateTimeOffset createdAfter,
        DateTimeOffset createdBefore,
        int take
    )
    {
        Query
            .Where(user =>
                user.WelcomeEmailSentAtUtc == null
                && user.CreatedAtUtc > createdAfter
                && user.CreatedAtUtc <= createdBefore
            )
            .OrderBy(user => user.CreatedAtUtc)
            .Take(take)
            .AsNoTracking();
        Query.Select(user => user.Id);
    }
}
