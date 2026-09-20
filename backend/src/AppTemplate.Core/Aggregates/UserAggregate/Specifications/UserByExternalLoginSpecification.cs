namespace AppTemplate.Core.Aggregates.UserAggregate.Specifications;

/// <summary>Looks up the user linked to a given OAuth provider identity (used by ExternalLoginHandler).</summary>
public class UserByExternalLoginSpecification : Specification<User>
{
    public UserByExternalLoginSpecification(string provider, string providerKey) =>
        Query
            .Where(user =>
                user.ExternalLogins.Any(login =>
                    login.Provider == provider && login.ProviderKey == providerKey
                )
            )
            .Include(user => user.ExternalLogins);
}
