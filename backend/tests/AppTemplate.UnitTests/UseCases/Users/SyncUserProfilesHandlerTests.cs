using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Caching;
using AppTemplate.UseCases.Users.SyncProfiles;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Users;

public class SyncUserProfilesHandlerTests
{
    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();
    private readonly ICacheInvalidator _cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly List<User> _users = [];
    private List<IdentityProviderUser> _identities = [];

    public SyncUserProfilesHandlerTests()
    {
        _repository
            .ListAsync(Arg.Any<UsersByExternalIdsSpecification>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
                _users
                    .Where(u => callInfo.Arg<UsersByExternalIdsSpecification>().IsSatisfiedBy(u))
                    .ToList()
            );
    }

    private User AddUser(string externalId, string name, string email)
    {
        var user = User.Create(externalId, UserName.From(name), new EmailAddress(email));
        _users.Add(user);
        return user;
    }

    private async Task<int> Sync()
    {
        var directory = Substitute.For<IIdentityProviderDirectory>();
        directory.ListUsersAsync(Arg.Any<CancellationToken>()).Returns(_ => ToAsync(_identities));
        var result = await new SyncUserProfilesHandler(
            directory,
            _repository,
            _cacheInvalidator
        ).Handle(new SyncUserProfilesCommand(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async IAsyncEnumerable<IdentityProviderUser> ToAsync(
        IEnumerable<IdentityProviderUser> items
    )
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    [Fact]
    public async Task Sync_CopiesChangedNamesAndEmails_AndInvalidatesTheCache()
    {
        var ada = AddUser("sub-ada", "Ada", "ada@example.com");
        _identities = [new("sub-ada", "Ada Lovelace", "ada.lovelace@example.com")];

        var updated = await Sync();

        Assert.Equal(1, updated);
        Assert.Equal("Ada Lovelace", ada.Name.Value);
        Assert.Equal("ada.lovelace@example.com", ada.Email.Value);
        await _repository
            .Received(1)
            .UpdateRangeAsync(
                Arg.Is<IEnumerable<User>>(users => users.Single() == ada),
                Arg.Any<CancellationToken>()
            );
        await _cacheInvalidator
            .Received(1)
            .InvalidateAsync(CacheTags.Users, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sync_WithNothingChanged_WritesNothing()
    {
        AddUser("sub-ada", "Ada", "ada@example.com");
        _identities = [new("sub-ada", "Ada", "ada@example.com"), new("sub-unknown", "X", null)];

        var updated = await Sync();

        Assert.Equal(0, updated);
        await _repository
            .DidNotReceive()
            .UpdateRangeAsync(Arg.Any<IEnumerable<User>>(), Arg.Any<CancellationToken>());
        await _cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sync_NeverTakesAnEmailAnotherUserAlreadyHas()
    {
        var ada = AddUser("sub-ada", "Ada", "ada@example.com");
        _repository
            .AnyAsync(Arg.Any<UserByEmailSpecification>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _identities = [new("sub-ada", "Ada", "taken@example.com")];

        var updated = await Sync();

        Assert.Equal(0, updated);
        Assert.Equal("ada@example.com", ada.Email.Value);
    }

    [Fact]
    public async Task Sync_IgnoresInvalidValuesFromTheProvider()
    {
        var ada = AddUser("sub-ada", "Ada", "ada@example.com");
        _identities = [new("sub-ada", new string('x', UserName.MaxLength + 1), "not-an-email")];

        var updated = await Sync();

        Assert.Equal(0, updated);
        Assert.Equal("Ada", ada.Name.Value);
    }

    [Fact]
    public async Task Sync_ProcessesLargeDirectoriesInBatches()
    {
        for (var i = 0; i < SyncUserProfilesHandler.BatchSize + 50; i++)
        {
            AddUser($"sub-{i}", $"User {i}", $"user{i}@example.com");
        }
        _identities = _users
            .Select(u => new IdentityProviderUser(u.ExternalId, $"{u.Name.Value} Renamed", null))
            .ToList();

        var updated = await Sync();

        Assert.Equal(SyncUserProfilesHandler.BatchSize + 50, updated);
        await _repository
            .Received(2)
            .ListAsync(Arg.Any<UsersByExternalIdsSpecification>(), Arg.Any<CancellationToken>());
    }
}
