using AppTemplate.UseCases.Jobs;
using Ardalis.Result;

namespace AppTemplate.Infrastructure.Jobs.Services;

/// <summary>
/// Used when <c>JobScheduling:Enabled</c> is false: there are no scheduled jobs, so lists are
/// empty and every job id is unknown. Restoring can't work without a scheduler.
/// </summary>
public sealed class DisabledJobManagementService : IJobManagementService
{
    public Task<IReadOnlyList<RecurringJobDto>> GetRecurringJobsAsync(
        CancellationToken cancellationToken
    ) => Task.FromResult<IReadOnlyList<RecurringJobDto>>([]);

    public Task<Result<RecurringJobDetailDto>> GetRecurringJobAsync(
        string jobId,
        CancellationToken cancellationToken
    ) => Task.FromResult<Result<RecurringJobDetailDto>>(Result.NotFound());

    public Task<Result> TriggerAsync(string jobId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.NotFound());

    public Task<Result> PauseAsync(string jobId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.NotFound());

    public Task<Result> ResumeAsync(string jobId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.NotFound());

    public Task<Result> RemoveAsync(string jobId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.NotFound());

    public Task<Result> RestoreAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Result.Unavailable("Job scheduling is disabled."));
}
