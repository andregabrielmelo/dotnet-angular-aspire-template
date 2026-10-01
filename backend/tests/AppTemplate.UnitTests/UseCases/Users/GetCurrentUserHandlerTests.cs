using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Users.GetCurrent;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Users;

public class GetCurrentUserHandlerTests
{
    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();

    private static readonly GetCurrentUserQuery Query = new("keycloak-sub-1");

    private GetCurrentUserHandler CreateHandler() => new(_repository, TestCaches.Create());

    private void GivenStoredUser(User? user) =>
        _repository
            .FirstOrDefaultAsync(
                Arg.Any<UserByExternalIdSpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(user);

    [Fact]
    public async Task Handle_WithExistingUser_ReturnsIt()
    {
        var user = User.Create(
            Query.ExternalId,
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com")
        );
        user.Id = UserId.From(1); // as if loaded from the database
        GivenStoredUser(user);

        var result = await CreateHandler().Handle(Query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Name, result.Value.Name);
        Assert.Equal(user.Email, result.Value.Email);
    }

    [Fact]
    public async Task Handle_WithUnknownIdentity_ReturnsNotFoundAndCreatesNothing()
    {
        GivenStoredUser(null);

        var result = await CreateHandler().Handle(Query, CancellationToken.None);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        await _repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingUser_IsServedFromTheCacheOnLaterCalls()
    {
        var user = User.Create(
            Query.ExternalId,
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com")
        );
        user.Id = UserId.From(1);
        GivenStoredUser(user);
        var handler = CreateHandler();

        await handler.Handle(Query, CancellationToken.None);
        var second = await handler.Handle(Query, CancellationToken.None);

        Assert.True(second.IsSuccess);
        await _repository
            .Received(1)
            .FirstOrDefaultAsync(
                Arg.Any<UserByExternalIdSpecification>(),
                Arg.Any<CancellationToken>()
            );
    }
}
