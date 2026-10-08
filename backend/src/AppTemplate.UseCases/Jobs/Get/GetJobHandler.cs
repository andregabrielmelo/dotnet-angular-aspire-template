namespace AppTemplate.UseCases.Jobs.Get;

/// <returns>The recurring job with its recent runs, or NotFound.</returns>
public record GetJobQuery(string JobId) : IQuery<Result<RecurringJobDetailDto>>;

public class GetJobHandler(IJobManagementService _jobs)
    : IQueryHandler<GetJobQuery, Result<RecurringJobDetailDto>>
{
    public async ValueTask<Result<RecurringJobDetailDto>> Handle(
        GetJobQuery request,
        CancellationToken cancellationToken
    ) => await _jobs.GetRecurringJobAsync(request.JobId, cancellationToken);
}
