using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Users.ForgotPassword;

public sealed record ForgotPasswordCommand(EmailAddress Email) : ICommand<Result>;

public sealed class ForgotPasswordHandler(IPasswordResetService _passwordResetService)
    : ICommandHandler<ForgotPasswordCommand, Result>
{
    public async ValueTask<Result> Handle(
        ForgotPasswordCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await _passwordResetService.SendPasswordResetEmailAsync(
            command.Email,
            cancellationToken
        );

        // An unknown email is reported as success too, so the endpoint can't be used to find
        // out which addresses have an account (user enumeration).
        return result.Status == ResultStatus.NotFound ? Result.Success() : result;
    }
}
