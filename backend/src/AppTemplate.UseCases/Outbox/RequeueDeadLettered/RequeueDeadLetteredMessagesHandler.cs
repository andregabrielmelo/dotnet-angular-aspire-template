namespace AppTemplate.UseCases.Outbox.RequeueDeadLettered;

public sealed class RequeueDeadLetteredMessagesHandler(IOutboxAdministration _outbox)
    : ICommandHandler<RequeueDeadLetteredMessagesCommand, Result<int>>
{
    public async ValueTask<Result<int>> Handle(
        RequeueDeadLetteredMessagesCommand command,
        CancellationToken cancellationToken
    ) => Result.Success(await _outbox.RequeueDeadLetteredAsync(cancellationToken));
}
