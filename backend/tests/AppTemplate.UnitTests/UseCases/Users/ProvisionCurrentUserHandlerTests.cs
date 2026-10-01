using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Caching;
using AppTemplate.UseCases.Jobs;
using AppTemplate.UseCases.Users.ProvisionCurrent;
using Ardalis.Result;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AppTemplate.UnitTests.UseCases.Users;

public class ProvisionCurrentUserHandlerTests
{
    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();
    private readonly ICacheInvalidator _cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly IBackgroundJobScheduler _jobs = Substitute.For<IBackgroundJobScheduler>();

    private static readonly ProvisionCurrentUserCommand Command = new(
        "keycloak-sub-1",
        UserName.From("Ada Lovelace"),
        new EmailAddress("ada@example.com")
    );

    private ProvisionCurrentUserHandler CreateHandler() =>
        new(_repository, _cacheInvalidator, _jobs);

    private static User StoredUser()
    {
        var user = User.Create(Command.ExternalId, Command.Name, Command.Email);
        user.Id = UserId.From(1); // as if loaded from the database
        return user;
    }

    private void GivenStoredUsers(params User?[] usersOnEachLookup) =>
        _repository
            .FirstOrDefaultAsync(
                Arg.Any<UserByExternalIdSpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(usersOnEachLookup[0], usersOnEachLookup[1..]);

    [Fact]
    public async Task Handle_WithNewIdentity_CreatesUserAndInvalidatesTheCache()
    {
        GivenStoredUsers((User?)null);
        _repository
            .AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<User>());

        var result = await CreateHandler().Handle(Command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Created);
        await _repository
            .Received(1)
            .AddAsync(
                Arg.Is<User>(u => u.ExternalId == Command.ExternalId && u.Email == Command.Email),
                Arg.Any<CancellationToken>()
            );
        await _cacheInvalidator
            .Received(1)
            .InvalidateAsync(CacheTags.Users, Arg.Any<CancellationToken>());
        Assert.Single(
            _jobs.ReceivedCalls(),
            call => call.GetMethodInfo().Name == nameof(IBackgroundJobScheduler.EnqueueWelcomeEmail)
        );
    }

    [Fact]
    public async Task Handle_WithExistingUser_ReturnsItWithoutCreating()
    {
        GivenStoredUsers(StoredUser());

        var result = await CreateHandler().Handle(Command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Created);
        Assert.Equal(Command.Name, result.Value.User.Name);
        await _repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        Assert.Empty(_jobs.ReceivedCalls());
    }

    [Fact]
    public async Task Handle_WithEmailLinkedToAnotherIdentity_ReturnsConflict()
    {
        GivenStoredUsers(null, null);
        _repository
            .AnyAsync(Arg.Any<UserByEmailSpecification>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateHandler().Handle(Command, CancellationToken.None);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        await _repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTheEmailCheckSeesAConcurrentInsertOfTheSameIdentity_ReturnsThatUser()
    {
        // Not there at the first lookup, but committed (with this email) before the email check.
        var winner = StoredUser();
        GivenStoredUsers(null, winner);
        _repository
            .AnyAsync(Arg.Any<UserByEmailSpecification>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateHandler().Handle(Command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Created);
        Assert.Equal(winner.Id, result.Value.User.Id);
        await _repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAConcurrentRequestProvisionedTheSameIdentity_ReturnsThatUser()
    {
        // Not there when checked, but inserted by someone else before this insert ran.
        var winner = StoredUser();
        GivenStoredUsers(null, winner);
        _repository
            .AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UniqueConstraintViolationException("ix_users_external_id", new()));

        var result = await CreateHandler().Handle(Command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Created);
        Assert.Equal(winner.Id, result.Value.User.Id);
        await _cacheInvalidator
            .Received(1)
            .InvalidateAsync(CacheTags.Users, Arg.Any<CancellationToken>());
        Assert.Empty(_jobs.ReceivedCalls()); // the winner enqueued it
    }

    [Fact]
    public async Task Handle_WhenAConcurrentRequestClaimedTheEmail_ReturnsConflict()
    {
        GivenStoredUsers(null, null);
        _repository
            .AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UniqueConstraintViolationException("ix_users_email", new()));

        var result = await CreateHandler().Handle(Command, CancellationToken.None);

        Assert.Equal(ResultStatus.Conflict, result.Status);
    }
}
