namespace AppTemplate.Core.Aggregates.UserAggregate.Specifications;

/// <summary>
/// Looks up the user owning a given email-confirmation/password-reset token hash. Also
/// includes RefreshTokens: ResetPasswordHandler needs both collections loaded on the same
/// aggregate instance to consume the reset token and revoke every outstanding session in one
/// round trip.
/// </summary>
public class UserByTokenHashSpecification : Specification<User>
{
    public UserByTokenHashSpecification(string tokenHash, UserTokenPurpose purpose) =>
        Query
            .Where(user =>
                user.Tokens.Any(token => token.TokenHash == tokenHash && token.Purpose == purpose)
            )
            .Include(user => user.Tokens)
            .Include(user => user.RefreshTokens);
}
