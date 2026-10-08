namespace AppTemplate.UseCases.Jobs.Resume;

/// <summary>Scheduled runs happen again. Resuming a job that isn't paused is a no-op.</summary>
public record ResumeJobCommand(string JobId) : Mediator.ICommand<Result>;

public class ResumeJobHandler(IJobManagementService _jobs)
    : Mediator.ICommandHandler<ResumeJobCommand, Result>
{
    public async ValueTask<Result> Handle(
        ResumeJobCommand request,
        CancellationToken cancellationToken
    ) => await _jobs.ResumeAsync(request.JobId, cancellationToken);
}
