namespace AppTemplate.UseCases.Jobs.Trigger;

/// <summary>Runs the job now, without changing its schedule. Works while paused too.</summary>
public record TriggerJobCommand(string JobId) : Mediator.ICommand<Result>;

public class TriggerJobHandler(IJobManagementService _jobs)
    : Mediator.ICommandHandler<TriggerJobCommand, Result>
{
    public async ValueTask<Result> Handle(
        TriggerJobCommand request,
        CancellationToken cancellationToken
    ) => await _jobs.TriggerAsync(request.JobId, cancellationToken);
}
