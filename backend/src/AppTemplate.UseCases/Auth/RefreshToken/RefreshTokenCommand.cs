using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.Interfaces;

namespace AppTemplate.UseCases.Auth.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : ICommand<Result<AuthResultDto>>;

public class RefreshTokenHandler(IRepository<User> _userRepository, IJwtTokenService _tokenService)
    : ICommandHandler<RefreshTokenCommand, Result<AuthResultDto>>
{
    public async ValueTask<Result<AuthResultDto>> Handle(
        RefreshTokenCommand command,
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
            return Result<AuthResultDto>.Unauthorized();
        }

        var token = user.RefreshTokens.First(t => t.TokenHash == tokenHash);
        var now = DateTimeOffset.UtcNow;

        if (token.RevokedAtUtc is not null)
        {
            // The token was already rotated away and is being presented again - a signal that
            // it (or a token from the same family) may have been stolen. Kill every session.
            user.RevokeAllRefreshTokens();
            await _userRepository.UpdateAsync(user, cancellationToken);
            return Result<AuthResultDto>.Unauthorized();
        }

        if (!token.IsActive(now))
        {
            return Result<AuthResultDto>.Unauthorized();
        }

        var authResult = TokenIssuer.IssueTokenPair(user, _tokenService);
        user.RevokeRefreshToken(token, TokenHasher.Hash(authResult.RefreshToken));
        await _userRepository.UpdateAsync(user, cancellationToken);

        return authResult;
    }
}
