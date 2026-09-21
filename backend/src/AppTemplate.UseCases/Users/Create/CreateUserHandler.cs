using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Users.Create;

public record CreateUserCommand(UserName Name, EmailAddress Email, string PhoneNumber)
    : ICommand<Result<UserId>>;

public class CreateUserHandler(IRepository<User> _userRepository)
    : ICommandHandler<CreateUserCommand, Result<UserId>>
{
    public async ValueTask<Result<UserId>> Handle(
        CreateUserCommand command,
        CancellationToken cancellationToken
    )
    {
        // TODO: Review if this is really the place to check for existing user, or if it should be done in the domain layer
        var existingUser = await _userRepository.FirstOrDefaultAsync(
            new UserByEmailSpecification(command.Email),
            cancellationToken
        );
        if (existingUser is not null)
        {
            return Result<UserId>.Invalid(
                new ValidationError(nameof(command.Email), "Email is already registered")
            );
        }

        var newUser = User.Create(command.Name, command.Email);
        if (!string.IsNullOrEmpty(command.PhoneNumber))
        {
            var phoneNumber = new PhoneNumber("+1", command.PhoneNumber, String.Empty);
            newUser.UpdatePhoneNumber(phoneNumber);
        }
        var createdItem = await _userRepository.AddAsync(newUser, cancellationToken);

        return createdItem.Id;
    }
}
