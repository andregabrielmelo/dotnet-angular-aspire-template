namespace AppTemplate.UseCases.Auth.Refresh;

public record RefreshCommand(string RefreshToken) : ICommand<Result<AuthTokensDto>>;

public class RefreshHandler(IIdentityService _identityService)
    : ICommandHandler<RefreshCommand, Result<AuthTokensDto>>
{
    public ValueTask<Result<AuthTokensDto>> Handle(
        RefreshCommand command,
        CancellationToken cancellationToken
    ) => new(_identityService.RefreshAsync(command.RefreshToken, cancellationToken));
}
