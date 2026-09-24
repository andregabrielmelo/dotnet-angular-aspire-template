using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Users.Get;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Users;

public class GetUserHandlerTests
{
    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();

    private static readonly GetUserQuery Query = new(UserId.From(1));

    private static User CreateUser()
    {
        var user = User.Create(
            "sub-1",
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com")
        );
        user.UpdatePhoneNumber(new PhoneNumber("+44", "5551234", "7"));
        user.Id = UserId.From(1); // as if loaded from the database
        return user;
    }

    private void RepositoryReturns(User? user) =>
        _repository
            .FirstOrDefaultAsync(Arg.Any<UserByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(user);

    private Task RepositoryWasQueried(int times) =>
        _repository
            .Received(times)
            .FirstOrDefaultAsync(Arg.Any<UserByIdSpecification>(), Arg.Any<CancellationToken>());

    [Fact]
    public async Task Handle_SecondRead_IsServedFromTheCache()
    {
        RepositoryReturns(CreateUser());
        var handler = new GetUserHandler(_repository, TestCaches.Create());

        var first = await handler.Handle(Query, CancellationToken.None);
        var second = await handler.Handle(Query, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value, second.Value);
        await RepositoryWasQueried(1);
    }

    [Fact]
    public async Task Handle_MissingUser_IsNotFoundAndCachedToo()
    {
        RepositoryReturns(null);
        var handler = new GetUserHandler(_repository, TestCaches.Create());

        var first = await handler.Handle(Query, CancellationToken.None);
        var second = await handler.Handle(Query, CancellationToken.None);

        Assert.Equal(ResultStatus.NotFound, first.Status);
        Assert.Equal(ResultStatus.NotFound, second.Status);
        await RepositoryWasQueried(1);
    }

    [Fact]
    public async Task Handle_OnAnotherInstance_ReadsTheSerializedEntryFromTheDistributedCache()
    {
        RepositoryReturns(CreateUser());
        var sharedL2 = TestCaches.CreateSharedDistributedCache();

        var fromDatabase = await new GetUserHandler(
            _repository,
            TestCaches.Create(sharedL2)
        ).Handle(Query, CancellationToken.None);
        var fromL2 = await new GetUserHandler(_repository, TestCaches.Create(sharedL2)).Handle(
            Query,
            CancellationToken.None
        );

        await RepositoryWasQueried(1);
        Assert.Equal(fromDatabase.Value.Name, fromL2.Value.Name);
        Assert.Equal(fromDatabase.Value.PhoneNumber, fromL2.Value.PhoneNumber);
        Assert.Equal("+44 5551234 x7", fromL2.Value.PhoneNumber!.ToString());
    }
}
