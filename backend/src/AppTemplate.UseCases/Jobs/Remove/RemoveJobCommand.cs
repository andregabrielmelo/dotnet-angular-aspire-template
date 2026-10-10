using AppTemplate.UseCases.Auditing;

namespace AppTemplate.UseCases.Jobs.Remove;

/// <summary>Removes the job from the scheduler until the next restore or restart.</summary>
public record RemoveJobCommand(string JobId) : Mediator.ICommand<Result>, IAuditedCommand
{
    string IAuditedCommand.AuditAction => AuditActions.JobRemoved;

    AuditTarget IAuditedCommand.AuditTarget => AuditTarget.Job(JobId);
}
