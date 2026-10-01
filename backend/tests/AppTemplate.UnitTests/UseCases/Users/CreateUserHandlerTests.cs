using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Caching;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.Create;
using Ardalis.Result;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Users;

public class CreateUserHandlerTests
{
    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();
    private readonly IPasswordHasher<User> _passwordHasher = Substitute.For<
        IPasswordHasher<User>
    >();
    private readonly ICache _cache = Substitute.For<ICache>();

    private CreateUserHandler CreateHandler() => new(_repository, _passwordHasher, _cache);

    [Fact]
    public async Task Handle_WithNewEmail_CreatesUser()
    {
        _repository
            .FirstOrDefaultAsync(Arg.Any<UserByEmailSpecification>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _repository
            .AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var user = callInfo.Arg<User>();
                user.Id = UserId.From(1);
                return Task.FromResult(user);
            });
        _passwordHasher.HashPassword(Arg.Any<User>(), "Passw0rd!").Returns("hashed-password");

        var command = new CreateUserCommand(
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com"),
            "Passw0rd!",
            ""
        );

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await _repository
            .Received(1)
            .AddAsync(
                Arg.Is<User>(u =>
                    u.Name.Value == "Ada Lovelace" && u.Password == "hashed-password"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Handle_WithNewEmail_InvalidatesCachedMissForNewId()
    {
        _repository
            .FirstOrDefaultAsync(Arg.Any<UserByEmailSpecification>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _repository
            .AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var user = callInfo.Arg<User>();
                user.Id = UserId.From(7);
                return Task.FromResult(user);
            });

        var command = new CreateUserCommand(
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com"),
            "Passw0rd!",
            ""
        );

        await CreateHandler().Handle(command, CancellationToken.None);

        await _cache
            .Received(1)
            .RemoveAsync(UserCacheKeys.ById(UserId.From(7)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithAlreadyRegisteredEmail_ReturnsInvalidWithoutCreating()
    {
        var existingUser = User.Create(
            UserName.From("Existing User"),
            new EmailAddress("ada@example.com"),
            "hash"
        );
        _repository
            .FirstOrDefaultAsync(Arg.Any<UserByEmailSpecification>(), Arg.Any<CancellationToken>())
            .Returns(existingUser);

        var command = new CreateUserCommand(
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com"),
            "Passw0rd!",
            ""
        );

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        await _repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _cache.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
