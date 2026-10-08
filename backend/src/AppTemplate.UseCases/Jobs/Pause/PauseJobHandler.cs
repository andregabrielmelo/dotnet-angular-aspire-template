namespace AppTemplate.UseCases.Jobs.Pause;

/// <summary>Skips scheduled runs until resumed. Pausing twice is a no-op.</summary>
public record PauseJobCommand(string JobId) : Mediator.ICommand<Result>;

public class PauseJobHandler(IJobManagementService _jobs)
    : Mediator.ICommandHandler<PauseJobCommand, Result>
{
    public async ValueTask<Result> Handle(
        PauseJobCommand request,
        CancellationToken cancellationToken
    ) => await _jobs.PauseAsync(request.JobId, cancellationToken);
}
