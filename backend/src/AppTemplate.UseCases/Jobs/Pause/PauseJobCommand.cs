namespace AppTemplate.UseCases.Jobs.Pause;

/// <summary>Skips scheduled runs until resumed. Pausing twice is a no-op.</summary>
public record PauseJobCommand(string JobId) : Mediator.ICommand<Result>;
