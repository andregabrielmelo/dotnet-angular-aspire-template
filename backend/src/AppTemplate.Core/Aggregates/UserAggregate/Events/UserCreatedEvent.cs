namespace AppTemplate.Core.Aggregates.UserAggregate.Events;

/// <summary>
/// Raised by <see cref="User.Create"/>. Carries the entity rather than its id: the id is only
/// assigned when the user is added to the DbContext, and events are dispatched after the save,
/// so handlers read <see cref="User.Id"/> once it is set.
/// </summary>
public sealed class UserCreatedEvent(User user) : DomainEventBase
{
    public User User { get; } = user;
}
