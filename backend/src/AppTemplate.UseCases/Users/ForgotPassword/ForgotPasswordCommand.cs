using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Users.ForgotPassword;

public sealed record ForgotPasswordCommand(EmailAddress Email) : ICommand<Result>;
