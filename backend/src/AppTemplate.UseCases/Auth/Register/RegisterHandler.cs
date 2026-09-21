using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Auth.Register;

public record RegisterCommand(
    UserName Name,
    EmailAddress Email,
    string Password,
    string PhoneNumber
) : ICommand<Result<AuthTokensDto>>;

public class RegisterHandler(IRepository<User> _userRepository, IIdentityService _identityService)
    : ICommandHandler<RegisterCommand, Result<AuthTokensDto>>
{
    public async ValueTask<Result<AuthTokensDto>> Handle(
        RegisterCommand command,
        CancellationToken cancellationToken
    )
    {
        var existingUser = await _userRepository.FirstOrDefaultAsync(
            new UserByEmailSpecification(command.Email),
            cancellationToken
        );
        if (existingUser is not null)
        {
            return Result<AuthTokensDto>.Invalid(
                new ValidationError(nameof(command.Email), "Email is already registered")
            );
        }

        // Intentionally not persisted here - IIdentityService.RegisterAsync persists this
        // domain row together with its Identity credentials, in one transaction.
        var newUser = User.Create(command.Name, command.Email);
        if (!string.IsNullOrEmpty(command.PhoneNumber))
        {
            var phoneNumber = new PhoneNumber("+1", command.PhoneNumber, String.Empty);
            newUser.UpdatePhoneNumber(phoneNumber);
        }

        return await _identityService.RegisterAsync(newUser, command.Password, cancellationToken);
    }
}
