namespace AppTemplate.UseCases.Jobs.Get;

public class GetJobHandler(IJobManagementService _jobs)
    : IQueryHandler<GetJobQuery, Result<RecurringJobDetailDto>>
{
    public async ValueTask<Result<RecurringJobDetailDto>> Handle(
        GetJobQuery request,
        CancellationToken cancellationToken
    ) => await _jobs.GetRecurringJobAsync(request.JobId, cancellationToken);
}
