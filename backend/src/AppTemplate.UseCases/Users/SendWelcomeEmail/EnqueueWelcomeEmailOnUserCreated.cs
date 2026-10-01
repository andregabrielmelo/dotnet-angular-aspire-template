using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.UseCases.Jobs;

namespace AppTemplate.UseCases.Users.SendWelcomeEmail;

/// <summary>
/// Reacts to a new user by scheduling the welcome email, so whichever use case creates a user
/// doesn't have to remember to. Runs after the user is saved (see EventDispatchInterceptor)
/// and only enqueues - sending is slow and can fail, and must never hold up that request. If
/// the enqueue itself is lost, <see cref="EnqueueMissedWelcomeEmailsHandler"/> catches it.
/// </summary>
public sealed class EnqueueWelcomeEmailOnUserCreated(IBackgroundJobScheduler _jobs)
    : IDomainEventHandler<UserCreatedEvent>
{
    public ValueTask Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        _jobs.EnqueueWelcomeEmail(notification.User.Id);
        return ValueTask.CompletedTask;
    }
}
