using AppTemplate.UseCases.Auditing;

namespace AppTemplate.UseCases.Jobs.Restore;

/// <summary>
/// Syncs the scheduler with the job definitions in code, keeping paused jobs paused.
/// </summary>
public record RestoreJobsCommand : Mediator.ICommand<Result>, IAuditedCommand
{
    string IAuditedCommand.AuditAction => AuditActions.JobsRestored;

    AuditTarget IAuditedCommand.AuditTarget => new("job", "*");
}
