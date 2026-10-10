using AppTemplate.UseCases.Auditing;

namespace AppTemplate.UseCases.Jobs.Remove;

public class RemoveJobHandler(IJobManagementService _jobs, IAuditLog _audit)
    : Mediator.ICommandHandler<RemoveJobCommand, Result>
{
    public async ValueTask<Result> Handle(
        RemoveJobCommand request,
        CancellationToken cancellationToken
    )
    {
        var result = await _jobs.RemoveAsync(request.JobId, cancellationToken);
        await _audit.RecordAsync(
            AuditActions.JobRemoved,
            AuditTarget.Job(request.JobId),
            result,
            cancellationToken
        );
        return result;
    }
}
