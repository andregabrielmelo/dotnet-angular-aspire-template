using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;

namespace AppTemplate.UseCases.Auth.Logout;

public record LogoutCommand(string RefreshToken) : ICommand<Result>;

public class LogoutHandler(IRepository<User> _userRepository)
    : ICommandHandler<LogoutCommand, Result>
{
    public async ValueTask<Result> Handle(
        LogoutCommand command,
        CancellationToken cancellationToken
    )
    {
        var tokenHash = TokenHasher.Hash(command.RefreshToken);
        var user = await _userRepository.FirstOrDefaultAsync(
            new UserByRefreshTokenHashSpecification(tokenHash),
            cancellationToken
        );
        if (user is null)
        {
            // Possession of a valid token is what authorizes a logout - an unknown/already
            // expired token has nothing left to revoke, so this is still a successful no-op.
            return Result.Success();
        }

        var token = user.RefreshTokens.First(t => t.TokenHash == tokenHash);
        user.RevokeRefreshToken(token);
        await _userRepository.UpdateAsync(user, cancellationToken);

        return Result.Success();
    }
}
