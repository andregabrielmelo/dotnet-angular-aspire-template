using AppTemplate.UseCases.Auditing;

namespace AppTemplate.UseCases.Jobs.Restore;

public class RestoreJobsHandler(IJobManagementService _jobs, IAuditLog _audit)
    : Mediator.ICommandHandler<RestoreJobsCommand, Result>
{
    public async ValueTask<Result> Handle(
        RestoreJobsCommand request,
        CancellationToken cancellationToken
    )
    {
        var result = await _jobs.RestoreAsync(cancellationToken);
        await _audit.RecordAsync(
            AuditActions.JobsRestored,
            new AuditTarget("job", "*"),
            result,
            cancellationToken
        );
        return result;
    }
}
