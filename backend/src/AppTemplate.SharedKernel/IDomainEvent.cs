namespace AppTemplate.SharedKernel;

public interface IDomainEvent : INotification
{
    DateTime DateOccurred { get; }
}
