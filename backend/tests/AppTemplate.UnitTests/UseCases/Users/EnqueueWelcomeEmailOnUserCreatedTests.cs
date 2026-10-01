using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.UseCases.Jobs;
using AppTemplate.UseCases.Users.SendWelcomeEmail;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Users;

public class EnqueueWelcomeEmailOnUserCreatedTests
{
    [Fact]
    public async Task Handle_EnqueuesTheWelcomeEmailForTheNewUser()
    {
        var jobs = Substitute.For<IBackgroundJobScheduler>();
        var user = User.Create(
            "keycloak-sub-1",
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com")
        );
        user.Id = UserId.From(42); // assigned when saved, before events are dispatched

        await new EnqueueWelcomeEmailOnUserCreated(jobs).Handle(
            new UserCreatedEvent(user),
            CancellationToken.None
        );

        jobs.Received(1).EnqueueWelcomeEmail(UserId.From(42));
    }
}
