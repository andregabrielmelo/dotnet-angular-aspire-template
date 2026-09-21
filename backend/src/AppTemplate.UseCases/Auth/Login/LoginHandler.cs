namespace AppTemplate.UseCases.Auth.Login;

public record LoginCommand(string Email, string Password) : ICommand<Result<AuthTokensDto>>;

public class LoginHandler(IIdentityService _identityService)
    : ICommandHandler<LoginCommand, Result<AuthTokensDto>>
{
    public ValueTask<Result<AuthTokensDto>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken
    ) => new(_identityService.LoginAsync(command.Email, command.Password, cancellationToken));
}
