namespace AppTemplate.Core.Events;

/// <summary>
/// A stored object is no longer referenced (replaced or its owner deleted). Delivered by the
/// outbox, so the delete is retried until object storage accepts it.
/// </summary>
[IntegrationEvent("storage.file_orphaned", 1)]
public sealed record StoredFileOrphaned(string Key) : IIntegrationEvent;
