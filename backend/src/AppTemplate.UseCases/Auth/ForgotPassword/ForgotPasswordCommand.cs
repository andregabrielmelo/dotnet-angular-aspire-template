using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.Interfaces;
using AppTemplate.Core.ValueObjects;
using Microsoft.Extensions.Options;

namespace AppTemplate.UseCases.Auth.ForgotPassword;

public record ForgotPasswordCommand(EmailAddress Email) : ICommand<Result>;

public class ForgotPasswordHandler(
    IRepository<User> _userRepository,
    IEmailSender _emailSender,
    IOptions<FrontendOptions> _frontendOptions,
    IOptions<EmailOptions> _emailOptions
) : ICommandHandler<ForgotPasswordCommand, Result>
{
    public async ValueTask<Result> Handle(
        ForgotPasswordCommand command,
        CancellationToken cancellationToken
    )
    {
        var user = await _userRepository.FirstOrDefaultAsync(
            new UserByEmailSpecification(command.Email),
            cancellationToken
        );

        // Deliberately no distinguishable outcome for an unknown email - revealing whether an
        // account exists would be a user-enumeration leak. An OAuth-only account (no password
        // yet) is allowed through: this is how such a user sets a first-party password.
        if (user is null)
        {
            return Result.Success();
        }

        var rawToken = TokenHasher.GenerateRawToken();
        user.IssueToken(
            UserTokenPurpose.PasswordReset,
            TokenHasher.Hash(rawToken),
            DateTimeOffset.UtcNow.Add(
                TimeSpan.FromHours(_emailOptions.Value.ResetTokenLifetimeHours)
            )
        );
        await _userRepository.UpdateAsync(user, cancellationToken);

        var resetLink = $"{_frontendOptions.Value.BaseUrl}/auth/reset-password?token={rawToken}";
        await _emailSender.SendEmailAsync(
            command.Email.Value,
            _emailOptions.Value.FromAddress,
            "Reset your password",
            $"Reset your password by visiting: {resetLink}"
        );

        return Result.Success();
    }
}
