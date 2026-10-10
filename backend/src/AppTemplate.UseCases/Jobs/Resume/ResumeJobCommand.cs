using AppTemplate.UseCases.Auditing;

namespace AppTemplate.UseCases.Jobs.Resume;

/// <summary>Scheduled runs happen again. Resuming a job that isn't paused is a no-op.</summary>
public record ResumeJobCommand(string JobId) : Mediator.ICommand<Result>, IAuditedCommand
{
    string IAuditedCommand.AuditAction => AuditActions.JobResumed;

    AuditTarget IAuditedCommand.AuditTarget => AuditTarget.Job(JobId);
}
