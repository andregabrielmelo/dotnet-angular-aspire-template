using AppTemplate.UseCases.Auditing;

namespace AppTemplate.UseCases.Jobs.Trigger;

public class TriggerJobHandler(IJobManagementService _jobs, IAuditLog _audit)
    : Mediator.ICommandHandler<TriggerJobCommand, Result>
{
    public async ValueTask<Result> Handle(
        TriggerJobCommand request,
        CancellationToken cancellationToken
    )
    {
        var result = await _jobs.TriggerAsync(request.JobId, cancellationToken);
        await _audit.RecordAsync(
            AuditActions.JobTriggered,
            AuditTarget.Job(request.JobId),
            result,
            cancellationToken
        );
        return result;
    }
}
