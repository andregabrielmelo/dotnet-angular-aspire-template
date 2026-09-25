using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Jobs;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.JobFeatures;

public class GetJobEndpoint(IJobManagementService _jobs)
    : Endpoint<JobIdRequest, Results<Ok<RecurringJobDetailResponse>, NotFound, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("/admin/jobs/{JobId}");
        Policies(Permission.JobsRead);

        Summary(s =>
        {
            s.Summary = "Get a recurring job";
            s.Description = "The job's schedule and its most recent runs.";
            s.Responses[200] = "The job with its recent runs";
            s.Responses[400] = "Invalid job id";
            s.Responses[403] = $"Requires the {Permission.JobsRead} permission";
            s.Responses[404] = "No such recurring job";
        });

        Tags(JobEndpoints.Tag);
    }

    public override async Task<
        Results<Ok<RecurringJobDetailResponse>, NotFound, ProblemHttpResult>
    > ExecuteAsync(JobIdRequest request, CancellationToken cancellationToken)
    {
        var result = await _jobs.GetRecurringJobAsync(request.JobId, cancellationToken);
        return result.ToGetByIdResult(RecurringJobDetailResponse.From);
    }
}
