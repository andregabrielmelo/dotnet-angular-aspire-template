namespace AppTemplate.UseCases.Jobs.List;

public class ListJobsHandler(IJobManagementService _jobs)
    : IQueryHandler<ListJobsQuery, Result<IReadOnlyList<RecurringJobDto>>>
{
    public async ValueTask<Result<IReadOnlyList<RecurringJobDto>>> Handle(
        ListJobsQuery request,
        CancellationToken cancellationToken
    ) => Result.Success(await _jobs.GetRecurringJobsAsync(cancellationToken));
}
