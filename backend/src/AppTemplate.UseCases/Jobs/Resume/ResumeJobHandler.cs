using AppTemplate.UseCases.Auditing;

namespace AppTemplate.UseCases.Jobs.Resume;

public class ResumeJobHandler(IJobManagementService _jobs, IAuditLog _audit)
    : Mediator.ICommandHandler<ResumeJobCommand, Result>
{
    public async ValueTask<Result> Handle(
        ResumeJobCommand request,
        CancellationToken cancellationToken
    )
    {
        var result = await _jobs.ResumeAsync(request.JobId, cancellationToken);
        await _audit.RecordAsync(
            AuditActions.JobResumed,
            AuditTarget.Job(request.JobId),
            result,
            cancellationToken
        );
        return result;
    }
}
