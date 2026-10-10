using AppTemplate.UseCases.Auditing;

namespace AppTemplate.UseCases.Jobs.Trigger;

/// <summary>Runs the job now, without changing its schedule. Works while paused too.</summary>
public record TriggerJobCommand(string JobId) : Mediator.ICommand<Result>, IAuditedCommand
{
    string IAuditedCommand.AuditAction => AuditActions.JobTriggered;

    AuditTarget IAuditedCommand.AuditTarget => AuditTarget.Job(JobId);
}
