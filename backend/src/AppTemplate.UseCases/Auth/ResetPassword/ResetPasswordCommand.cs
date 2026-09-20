using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.UseCases.Auth;
using Microsoft.AspNetCore.Identity;

namespace AppTemplate.UseCases.Auth.ResetPassword;

public record ResetPasswordCommand(string Token, string NewPassword) : ICommand<Result>;

public class ResetPasswordHandler(
    IRepository<User> _userRepository,
    IPasswordHasher<User> _passwordHasher
) : ICommandHandler<ResetPasswordCommand, Result>
{
    public async ValueTask<Result> Handle(
        ResetPasswordCommand command,
        CancellationToken cancellationToken
    )
    {
        var tokenHash = TokenHasher.Hash(command.Token);
        var user = await _userRepository.FirstOrDefaultAsync(
            new UserByTokenHashSpecification(tokenHash, UserTokenPurpose.PasswordReset),
            cancellationToken
        );

        var token = user?.Tokens.FirstOrDefault(t => t.TokenHash == tokenHash);
        if (token is null || !token.IsValid(DateTimeOffset.UtcNow))
        {
            return Result.Invalid(
                new ValidationError(
                    nameof(command.Token),
                    new InvalidTokenException("This reset link is invalid or has expired.").Message
                )
            );
        }

        user!.UpdatePassword(_passwordHasher.HashPassword(user, command.NewPassword));
        user.ConsumeToken(token);
        // Kills every other outstanding session - the whole point of a password reset.
        user.RevokeAllRefreshTokens();
        await _userRepository.UpdateAsync(user, cancellationToken);

        return Result.Success();
    }
}
