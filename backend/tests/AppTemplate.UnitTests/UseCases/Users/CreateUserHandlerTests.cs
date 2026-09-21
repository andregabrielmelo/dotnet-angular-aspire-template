using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Users.Create;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Users;

public class CreateUserHandlerTests
{
    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();

    private CreateUserHandler CreateHandler() => new(_repository);

    [Fact]
    public async Task Handle_WithNewEmail_CreatesUser()
    {
        _repository
            .FirstOrDefaultAsync(Arg.Any<UserByEmailSpecification>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _repository
            .AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<User>()));

        var command = new CreateUserCommand(
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com"),
            ""
        );

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await _repository
            .Received(1)
            .AddAsync(
                Arg.Is<User>(u => u.Name.Value == "Ada Lovelace"),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Handle_WithAlreadyRegisteredEmail_ReturnsInvalidWithoutCreating()
    {
        var existingUser = User.Create(
            UserName.From("Existing User"),
            new EmailAddress("ada@example.com")
        );
        _repository
            .FirstOrDefaultAsync(Arg.Any<UserByEmailSpecification>(), Arg.Any<CancellationToken>())
            .Returns(existingUser);

        var command = new CreateUserCommand(
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com"),
            ""
        );

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        await _repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
