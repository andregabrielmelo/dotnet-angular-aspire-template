using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.Update;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Users;

public class UpdateUserHandlerTests
{
    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();
    private readonly FakeCache _cache = new();

    private UpdateUserHandler CreateHandler() => new(_repository, _cache);

    [Fact]
    public async Task Handle_WithExistingUser_UpdatesAndInvalidatesCache()
    {
        var user = User.Create(
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com"),
            "hash"
        );
        user.Id = UserId.From(1);
        _repository.GetByIdAsync(UserId.From(1), Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler()
            .Handle(
                new UpdateUserCommand(UserId.From(1), UserName.From("Ada King"), null),
                CancellationToken.None
            );

        Assert.True(result.IsSuccess);
        Assert.Equal("Ada King", result.Value.Name.Value);
        await _repository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        Assert.Equal([UserCacheKeys.ById(UserId.From(1))], _cache.RemovedKeys);
    }

    [Fact]
    public async Task Handle_WithMissingUser_ReturnsNotFoundWithoutInvalidating()
    {
        _repository.GetByIdAsync(UserId.From(1), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateHandler()
            .Handle(
                new UpdateUserCommand(UserId.From(1), UserName.From("Ada King"), null),
                CancellationToken.None
            );

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Empty(_cache.RemovedKeys);
    }
}
