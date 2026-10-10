namespace AppTemplate.UseCases.Jobs.Remove;

public class RemoveJobHandler(IJobManagementService _jobs)
    : Mediator.ICommandHandler<RemoveJobCommand, Result>
{
    public async ValueTask<Result> Handle(
        RemoveJobCommand request,
        CancellationToken cancellationToken
    ) => await _jobs.RemoveAsync(request.JobId, cancellationToken);
}
