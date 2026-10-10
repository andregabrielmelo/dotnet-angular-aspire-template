using AppTemplate.Core.Events;
using AppTemplate.UseCases.Telemetry;

namespace AppTemplate.UseCases.Files;

/// <summary>
/// Deletes an object nothing references any more. Delivered by the outbox: if storage is down,
/// the throw makes the outbox retry with back-off. Deleting a missing key succeeds, so a
/// redelivery is harmless.
/// </summary>
public sealed class DeleteOrphanedFileHandler(IFileStorage _storage, ApplicationMetrics _metrics)
    : INotificationHandler<StoredFileOrphaned>
{
    public async ValueTask Handle(
        StoredFileOrphaned notification,
        CancellationToken cancellationToken
    )
    {
        await _storage.DeleteAsync(notification.Key, cancellationToken);
        _metrics.FileDeleted();
    }
}
