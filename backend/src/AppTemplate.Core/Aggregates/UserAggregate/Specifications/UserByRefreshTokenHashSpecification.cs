namespace AppTemplate.Core.Aggregates.UserAggregate.Specifications;

/// <summary>Looks up the user owning a given refresh token hash (used by RefreshTokenHandler/LogoutHandler).</summary>
public class UserByRefreshTokenHashSpecification : Specification<User>
{
    public UserByRefreshTokenHashSpecification(string tokenHash) =>
        Query
            .Where(user => user.RefreshTokens.Any(token => token.TokenHash == tokenHash))
            .Include(user => user.RefreshTokens);
}
