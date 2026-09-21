namespace AppTemplate.UseCases.Auth.Logout;

public record LogoutCommand(string RefreshToken) : ICommand<Result>;

public class LogoutHandler(IIdentityService _identityService)
    : ICommandHandler<LogoutCommand, Result>
{
    public ValueTask<Result> Handle(LogoutCommand command, CancellationToken cancellationToken) =>
        new(_identityService.LogoutAsync(command.RefreshToken, cancellationToken));
}
