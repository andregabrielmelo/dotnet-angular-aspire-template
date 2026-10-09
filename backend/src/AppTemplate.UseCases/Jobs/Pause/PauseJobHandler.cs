namespace AppTemplate.UseCases.Jobs.Pause;

public class PauseJobHandler(IJobManagementService _jobs)
    : Mediator.ICommandHandler<PauseJobCommand, Result>
{
    public async ValueTask<Result> Handle(
        PauseJobCommand request,
        CancellationToken cancellationToken
    ) => await _jobs.PauseAsync(request.JobId, cancellationToken);
}
