using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.Interfaces;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Auth.ExternalLogin;

public record ExternalLoginCommand(
    string Provider,
    string ProviderKey,
    EmailAddress Email,
    UserName Name
) : ICommand<Result<AuthResultDto>>;

/// <summary>
/// Find-or-link-or-create by provider identity, then by email. Provider-agnostic (Provider is
/// just a string), so a second OAuth provider reuses this same handler.
/// </summary>
public class ExternalLoginHandler(IRepository<User> _userRepository, IJwtTokenService _tokenService)
    : ICommandHandler<ExternalLoginCommand, Result<AuthResultDto>>
{
    public async ValueTask<Result<AuthResultDto>> Handle(
        ExternalLoginCommand command,
        CancellationToken cancellationToken
    )
    {
        var user = await _userRepository.FirstOrDefaultAsync(
            new UserByExternalLoginSpecification(command.Provider, command.ProviderKey),
            cancellationToken
        );

        if (user is not null)
        {
            var existingAuthResult = TokenIssuer.IssueTokenPair(user, _tokenService);
            await _userRepository.UpdateAsync(user, cancellationToken);
            return existingAuthResult;
        }

        user = await _userRepository.FirstOrDefaultAsync(
            new UserByEmailSpecification(command.Email),
            cancellationToken
        );

        if (user is not null)
        {
            user.AddExternalLogin(command.Provider, command.ProviderKey);
            var linkedAuthResult = TokenIssuer.IssueTokenPair(user, _tokenService);
            await _userRepository.UpdateAsync(user, cancellationToken);
            return linkedAuthResult;
        }

        var newUser = User.CreateExternal(command.Name, command.Email);
        newUser.AddExternalLogin(command.Provider, command.ProviderKey);
        var newAuthResult = TokenIssuer.IssueTokenPair(newUser, _tokenService);
        await _userRepository.AddAsync(newUser, cancellationToken);

        return newAuthResult;
    }
}
