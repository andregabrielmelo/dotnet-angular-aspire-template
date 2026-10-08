namespace AppTemplate.UseCases.Jobs.Restore;

/// <summary>
/// Syncs the scheduler with the job definitions in code, keeping paused jobs paused.
/// </summary>
public record RestoreJobsCommand : Mediator.ICommand<Result>;

public class RestoreJobsHandler(IJobManagementService _jobs)
    : Mediator.ICommandHandler<RestoreJobsCommand, Result>
{
    public async ValueTask<Result> Handle(
        RestoreJobsCommand request,
        CancellationToken cancellationToken
    ) => await _jobs.RestoreAsync(cancellationToken);
}
