using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.Interfaces;
using AppTemplate.Core.ValueObjects;
using Microsoft.AspNetCore.Identity;

namespace AppTemplate.UseCases.Auth.Login;

public record LoginCommand(EmailAddress Email, string Password) : ICommand<Result<AuthResultDto>>;

public class LoginHandler(
    IRepository<User> _userRepository,
    IPasswordHasher<User> _passwordHasher,
    IJwtTokenService _tokenService
) : ICommandHandler<LoginCommand, Result<AuthResultDto>>
{
    public async ValueTask<Result<AuthResultDto>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken
    )
    {
        var user = await _userRepository.FirstOrDefaultAsync(
            new UserByEmailSpecification(command.Email),
            cancellationToken
        );

        // No such account, and an OAuth-only account (no password) attempting a password
        // login, collapse to the exact same outcome - revealing the difference would leak
        // that the email is registered and how.
        if (user is null || user.Password is null)
        {
            return Result<AuthResultDto>.Unauthorized();
        }

        var verification = _passwordHasher.VerifyHashedPassword(
            user,
            user.Password,
            command.Password
        );
        if (verification == PasswordVerificationResult.Failed)
        {
            return Result<AuthResultDto>.Unauthorized();
        }

        if (!user.EmailConfirmed)
        {
            return Result<AuthResultDto>.Forbidden();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.UpdatePassword(_passwordHasher.HashPassword(user, command.Password));
        }

        var authResult = TokenIssuer.IssueTokenPair(user, _tokenService);
        await _userRepository.UpdateAsync(user, cancellationToken);

        return authResult;
    }
}
