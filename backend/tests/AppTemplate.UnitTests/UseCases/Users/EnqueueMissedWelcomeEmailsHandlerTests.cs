using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Jobs;
using AppTemplate.UseCases.Users.SendWelcomeEmail;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Users;

public class EnqueueMissedWelcomeEmailsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();
    private readonly IBackgroundJobScheduler _jobs = Substitute.For<IBackgroundJobScheduler>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();

    public EnqueueMissedWelcomeEmailsHandlerTests() => _timeProvider.GetUtcNow().Returns(Now);

    private Task<Ardalis.Result.Result<int>> Handle() =>
        new EnqueueMissedWelcomeEmailsHandler(_repository, _jobs, _timeProvider)
            .Handle(new EnqueueMissedWelcomeEmailsCommand(), CancellationToken.None)
            .AsTask();

    [Fact]
    public async Task Handle_EnqueuesEveryUserTheSpecificationFinds()
    {
        _repository
            .ListAsync(
                Arg.Any<UsersAwaitingWelcomeEmailSpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns([UserId.From(1), UserId.From(2)]);

        var result = await Handle();

        Assert.Equal(2, result.Value);
        _jobs.Received(1).EnqueueWelcomeEmail(UserId.From(1));
        _jobs.Received(1).EnqueueWelcomeEmail(UserId.From(2));
    }

    [Fact]
    public async Task Handle_WithNothingMissed_EnqueuesNothing()
    {
        _repository
            .ListAsync(
                Arg.Any<UsersAwaitingWelcomeEmailSpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new List<UserId>());

        var result = await Handle();

        Assert.Equal(0, result.Value);
        Assert.Empty(_jobs.ReceivedCalls());
    }

    [Fact]
    public void Specification_SelectsOnlyUnsentUsersInsideTheWindow()
    {
        var after = Now - EnqueueMissedWelcomeEmailsHandler.MaxAge;
        var before = Now - EnqueueMissedWelcomeEmailsHandler.GracePeriod;
        var spec = new UsersAwaitingWelcomeEmailSpecification(after, before, take: 10);

        var tooNew = UserCreatedAt("too-new", Now.AddMinutes(-30));
        var inWindow = UserCreatedAt("in-window", Now.AddHours(-3));
        var tooOld = UserCreatedAt("too-old", Now.AddDays(-8));
        var alreadySent = UserCreatedAt("already-sent", Now.AddHours(-3));
        alreadySent.MarkWelcomeEmailSent(Now.AddHours(-3));

        Assert.True(spec.IsSatisfiedBy(inWindow));
        Assert.False(spec.IsSatisfiedBy(tooNew));
        Assert.False(spec.IsSatisfiedBy(tooOld));
        Assert.False(spec.IsSatisfiedBy(alreadySent));
    }

    /// <summary>The database sets CreatedAtUtc on insert; set it here the way EF would.</summary>
    private static User UserCreatedAt(string externalId, DateTimeOffset createdAtUtc)
    {
        var user = User.Create(
            externalId,
            UserName.From("Ada Lovelace"),
            new EmailAddress($"{externalId}@example.com")
        );
        typeof(User).GetProperty(nameof(User.CreatedAtUtc))!.SetValue(user, createdAtUtc);
        return user;
    }
}
