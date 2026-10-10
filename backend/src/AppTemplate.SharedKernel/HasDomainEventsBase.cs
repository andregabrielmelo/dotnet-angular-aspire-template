using System.ComponentModel.DataAnnotations.Schema;

namespace AppTemplate.SharedKernel;

public abstract class HasDomainEventsBase : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();
    private readonly List<IIntegrationEvent> _integrationEvents = new();

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Written to the outbox by the same <c>SaveChanges</c> that saves this entity.</summary>
    [NotMapped]
    public IReadOnlyCollection<IIntegrationEvent> IntegrationEvents =>
        _integrationEvents.AsReadOnly();

    protected void RegisterDomainEvent(DomainEventBase domainEvent) =>
        _domainEvents.Add(domainEvent);

    protected void RaiseIntegrationEvent(IIntegrationEvent integrationEvent) =>
        _integrationEvents.Add(integrationEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public void ClearIntegrationEvents() => _integrationEvents.Clear();
}
