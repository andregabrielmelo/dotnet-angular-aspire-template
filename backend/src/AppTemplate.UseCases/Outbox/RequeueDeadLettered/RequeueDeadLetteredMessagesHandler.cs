using AppTemplate.UseCases.Auditing;

namespace AppTemplate.UseCases.Outbox.RequeueDeadLettered;

public sealed class RequeueDeadLetteredMessagesHandler(
    IOutboxAdministration _outbox,
    IAuditLog _audit
) : ICommandHandler<RequeueDeadLetteredMessagesCommand, Result<int>>
{
    public async ValueTask<Result<int>> Handle(
        RequeueDeadLetteredMessagesCommand command,
        CancellationToken cancellationToken
    )
    {
        var requeued = await _outbox.RequeueDeadLetteredAsync(cancellationToken);
        await _audit.RecordAsync(
            AuditActions.OutboxDeadLettersRequeued,
            AuditTarget.Outbox(),
            AuditOutcome.Succeeded,
            cancellationToken
        );
        return Result.Success(requeued);
    }
}
