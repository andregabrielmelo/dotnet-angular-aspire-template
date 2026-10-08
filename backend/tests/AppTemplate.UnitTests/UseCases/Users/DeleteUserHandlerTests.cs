using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.Delete;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Users;

public class DeleteUserHandlerTests
{
    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();
    private readonly FakeCache _cache = new();

    [Fact]
    public async Task Handle_WithExistingUser_DeletesAndReturnsSuccess()
    {
        var user = User.Create(
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com"),
            "hash"
        );
        user.Id = UserId.From(1);
        _repository.GetByIdAsync(UserId.From(1), Arg.Any<CancellationToken>()).Returns(user);

        var result = await new DeleteUserHandler(_repository, _cache).Handle(
            new DeleteUserCommand(UserId.From(1)),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        await _repository.Received(1).DeleteAsync(user, Arg.Any<CancellationToken>());
        Assert.Equal([UserCacheKeys.ById(user.Id)], _cache.RemovedKeys);
    }

    [Fact]
    public async Task Handle_WithMissingUser_ReturnsNotFound()
    {
        _repository.GetByIdAsync(UserId.From(1), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await new DeleteUserHandler(_repository, _cache).Handle(
            new DeleteUserCommand(UserId.From(1)),
            CancellationToken.None
        );

        Assert.Equal(ResultStatus.NotFound, result.Status);
        await _repository
            .DidNotReceive()
            .DeleteAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        Assert.Empty(_cache.RemovedKeys);
    }
}
