namespace AppTemplate.UseCases.Jobs.Resume;

public class ResumeJobHandler(IJobManagementService _jobs)
    : Mediator.ICommandHandler<ResumeJobCommand, Result>
{
    public async ValueTask<Result> Handle(
        ResumeJobCommand request,
        CancellationToken cancellationToken
    ) => await _jobs.ResumeAsync(request.JobId, cancellationToken);
}
