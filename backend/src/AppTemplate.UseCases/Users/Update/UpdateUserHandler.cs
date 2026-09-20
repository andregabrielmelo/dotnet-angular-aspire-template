using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Users.Update;

public record UpdateUserCommand(UserId UserId, UserName UserName, string? PhoneNumber)
    : Mediator.ICommand<Result<UserDto>>;

public class UpdateUserHandler(IRepository<User> _repository)
    : Mediator.ICommandHandler<UpdateUserCommand, Result<UserDto>>
{
    public async ValueTask<Result<UserDto>> Handle(
        UpdateUserCommand command,
        CancellationToken cancellationToken
    )
    {
        var user = await _repository.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null)
            return Result<UserDto>.NotFound();

        user.UpdateName(command.UserName);
        if (!string.IsNullOrEmpty(command.PhoneNumber))
        {
            var phoneNumber = new PhoneNumber("+1", command.PhoneNumber, String.Empty);
            user.UpdatePhoneNumber(phoneNumber);
        }

        await _repository.UpdateAsync(user, cancellationToken);

        var dto = new UserDto(user.Id, user.Name, user.PhoneNumber);
        return Result<UserDto>.Success(dto);
    }
}
