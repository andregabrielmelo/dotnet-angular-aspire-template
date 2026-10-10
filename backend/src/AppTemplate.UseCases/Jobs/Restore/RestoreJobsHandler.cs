namespace AppTemplate.UseCases.Jobs.Restore;

public class RestoreJobsHandler(IJobManagementService _jobs)
    : Mediator.ICommandHandler<RestoreJobsCommand, Result>
{
    public async ValueTask<Result> Handle(
        RestoreJobsCommand request,
        CancellationToken cancellationToken
    ) => await _jobs.RestoreAsync(cancellationToken);
}
