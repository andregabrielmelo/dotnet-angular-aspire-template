using AppTemplate.UseCases.Auditing;

namespace AppTemplate.UseCases.Jobs.Pause;

public class PauseJobHandler(IJobManagementService _jobs, IAuditLog _audit)
    : Mediator.ICommandHandler<PauseJobCommand, Result>
{
    public async ValueTask<Result> Handle(
        PauseJobCommand request,
        CancellationToken cancellationToken
    )
    {
        var result = await _jobs.PauseAsync(request.JobId, cancellationToken);
        await _audit.RecordAsync(
            AuditActions.JobPaused,
            AuditTarget.Job(request.JobId),
            result,
            cancellationToken
        );
        return result;
    }
}
