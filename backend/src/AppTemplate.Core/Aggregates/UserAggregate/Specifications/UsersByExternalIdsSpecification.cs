namespace AppTemplate.Core.Aggregates.UserAggregate.Specifications;

/// <summary>Tracked lookup of many users by their identity-provider subjects (for batch updates).</summary>
public class UsersByExternalIdsSpecification : Specification<User>
{
    public UsersByExternalIdsSpecification(IReadOnlyCollection<string> externalIds) =>
        Query.Where(user => externalIds.Contains(user.ExternalId));
}
