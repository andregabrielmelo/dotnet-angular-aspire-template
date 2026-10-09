namespace AppTemplate.UseCases.Jobs.Restore;

/// <summary>
/// Syncs the scheduler with the job definitions in code, keeping paused jobs paused.
/// </summary>
public record RestoreJobsCommand : Mediator.ICommand<Result>;
