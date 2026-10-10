using AppTemplate.UseCases.Auditing;

namespace AppTemplate.UseCases.Outbox.RequeueDeadLettered;

/// <summary>Retry every dead-lettered outbox message, after the cause (a bug, an outage) is fixed.</summary>
public sealed record RequeueDeadLetteredMessagesCommand : ICommand<Result<int>>, IAuditedCommand
{
    string IAuditedCommand.AuditAction => AuditActions.OutboxDeadLettersRequeued;

    AuditTarget IAuditedCommand.AuditTarget => AuditTarget.Outbox();
}
