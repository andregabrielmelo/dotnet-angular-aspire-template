namespace AppTemplate.UseCases.Jobs.Remove;

/// <summary>Removes the job from the scheduler until the next restore or restart.</summary>
public record RemoveJobCommand(string JobId) : Mediator.ICommand<Result>;

public class RemoveJobHandler(IJobManagementService _jobs)
    : Mediator.ICommandHandler<RemoveJobCommand, Result>
{
    public async ValueTask<Result> Handle(
        RemoveJobCommand request,
        CancellationToken cancellationToken
    ) => await _jobs.RemoveAsync(request.JobId, cancellationToken);
}
