namespace AppTemplate.Core.Aggregates.UserAggregate.Events;

/// <summary>
/// A user row was created for an identity signing in for the first time. Carries the
/// Keycloak <c>sub</c> rather than the row id: it identifies the user just as well, and it's
/// known when the event is raised, before the row is saved.
/// </summary>
[IntegrationEvent("user.provisioned", 1)]
public sealed record UserProvisioned(string ExternalId) : IIntegrationEvent;
