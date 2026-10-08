namespace AppTemplate.UseCases.Jobs.List;

/// <summary>Every scheduled recurring job. Not paged: there are only a handful.</summary>
public record ListJobsQuery : IQuery<Result<IReadOnlyList<RecurringJobDto>>>;

public class ListJobsHandler(IJobManagementService _jobs)
    : IQueryHandler<ListJobsQuery, Result<IReadOnlyList<RecurringJobDto>>>
{
    public async ValueTask<Result<IReadOnlyList<RecurringJobDto>>> Handle(
        ListJobsQuery request,
        CancellationToken cancellationToken
    ) => Result.Success(await _jobs.GetRecurringJobsAsync(cancellationToken));
}
