namespace AppTemplate.UseCases.Outbox.RequeueDeadLettered;

/// <summary>Retry every dead-lettered outbox message, after the cause (a bug, an outage) is fixed.</summary>
public sealed record RequeueDeadLetteredMessagesCommand : ICommand<Result<int>>;
