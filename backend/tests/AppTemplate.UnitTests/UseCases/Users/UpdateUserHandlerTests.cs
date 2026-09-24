using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Caching;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.Update;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Users;

public class UpdateUserHandlerTests
{
    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ICacheInvalidator _cacheInvalidator = Substitute.For<ICacheInvalidator>();

    private readonly User _target = User.Create(
        "owner-sub",
        UserName.From("Ada Lovelace"),
        new EmailAddress("ada@example.com")
    );

    private static readonly UpdateUserCommand Command = new(
        UserId.From(1),
        UserName.From("Grace Hopper"),
        null
    );

    public UpdateUserHandlerTests()
    {
        _repository.GetByIdAsync(Command.UserId, Arg.Any<CancellationToken>()).Returns(_target);
    }

    private Task<Result<UserDto>> Handle() =>
        new UpdateUserHandler(_repository, _currentUser, _cacheInvalidator)
            .Handle(Command, CancellationToken.None)
            .AsTask();

    [Fact]
    public async Task Handle_OwnProfile_UpdatesWithoutPermissions()
    {
        _currentUser.ExternalId.Returns("owner-sub");

        var result = await Handle();

        Assert.True(result.IsSuccess);
        Assert.Equal(Command.UserName, _target.Name);
        await _repository.Received(1).UpdateAsync(_target, Arg.Any<CancellationToken>());
        await _cacheInvalidator
            .Received(1)
            .InvalidateAsync(CacheTags.Users, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SomeoneElse_WithoutUsersWrite_IsForbiddenAndChangesNothing()
    {
        _currentUser.ExternalId.Returns("another-sub");
        _currentUser.HasPermission(Permission.UsersWrite).Returns(false);

        var result = await Handle();

        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Equal("Ada Lovelace", _target.Name.Value);
        await _repository
            .DidNotReceive()
            .UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SomeoneElse_WithUsersWrite_Updates()
    {
        _currentUser.ExternalId.Returns("admin-sub");
        _currentUser.HasPermission(Permission.UsersWrite).Returns(true);

        var result = await Handle();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_MissingUser_ReturnsNotFound()
    {
        _repository.GetByIdAsync(Command.UserId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await Handle();

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }
}
