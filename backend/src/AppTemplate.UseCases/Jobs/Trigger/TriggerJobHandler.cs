namespace AppTemplate.UseCases.Jobs.Trigger;

public class TriggerJobHandler(IJobManagementService _jobs)
    : Mediator.ICommandHandler<TriggerJobCommand, Result>
{
    public async ValueTask<Result> Handle(
        TriggerJobCommand request,
        CancellationToken cancellationToken
    ) => await _jobs.TriggerAsync(request.JobId, cancellationToken);
}
