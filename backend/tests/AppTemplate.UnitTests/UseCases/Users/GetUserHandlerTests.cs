using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.Get;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Users;

public class GetUserHandlerTests
{
    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();
    private readonly FakeCache _cache = new();

    private GetUserHandler CreateHandler() => new(_repository, _cache);

    [Fact]
    public async Task Handle_OnCacheMiss_LoadsFromRepositoryAndCachesIt()
    {
        var user = User.Create(
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com"),
            "hash"
        );
        user.Id = UserId.From(1);
        _repository
            .FirstOrDefaultAsync(Arg.Any<UserByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await CreateHandler()
            .Handle(new GetUserQuery(UserId.From(1)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ada Lovelace", result.Value.Name.Value);
        Assert.True(_cache.Contains(UserCacheKeys.ById(UserId.From(1))));
        await _repository
            .Received(1)
            .FirstOrDefaultAsync(Arg.Any<UserByIdSpecification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OnCacheHit_DoesNotQueryRepository()
    {
        var cached = new UserDto(UserId.From(1), UserName.From("Ada Lovelace"), null);
        _cache.Seed<UserDto?>(UserCacheKeys.ById(UserId.From(1)), cached);

        var result = await CreateHandler()
            .Handle(new GetUserQuery(UserId.From(1)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(cached, result.Value);
        await _repository
            .DidNotReceive()
            .FirstOrDefaultAsync(Arg.Any<UserByIdSpecification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsNotFoundWithoutCachingTheMiss()
    {
        _repository
            .FirstOrDefaultAsync(Arg.Any<UserByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await CreateHandler()
            .Handle(new GetUserQuery(UserId.From(1)), CancellationToken.None);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.False(_cache.Contains(UserCacheKeys.ById(UserId.From(1))));
    }
}
