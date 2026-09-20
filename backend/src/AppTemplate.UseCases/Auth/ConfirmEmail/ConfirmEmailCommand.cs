using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.UseCases.Auth;

namespace AppTemplate.UseCases.Auth.ConfirmEmail;

public record ConfirmEmailCommand(string Token) : ICommand<Result>;

public class ConfirmEmailHandler(IRepository<User> _userRepository)
    : ICommandHandler<ConfirmEmailCommand, Result>
{
    public async ValueTask<Result> Handle(
        ConfirmEmailCommand command,
        CancellationToken cancellationToken
    )
    {
        var tokenHash = TokenHasher.Hash(command.Token);
        var user = await _userRepository.FirstOrDefaultAsync(
            new UserByTokenHashSpecification(tokenHash, UserTokenPurpose.EmailConfirmation),
            cancellationToken
        );

        var token = user?.Tokens.FirstOrDefault(t => t.TokenHash == tokenHash);
        if (token is null || !token.IsValid(DateTimeOffset.UtcNow))
        {
            return Result.Invalid(
                new ValidationError(nameof(command.Token), new InvalidTokenException().Message)
            );
        }

        user!.ConfirmEmail();
        user.ConsumeToken(token);
        await _userRepository.UpdateAsync(user, cancellationToken);

        return Result.Success();
    }
}
