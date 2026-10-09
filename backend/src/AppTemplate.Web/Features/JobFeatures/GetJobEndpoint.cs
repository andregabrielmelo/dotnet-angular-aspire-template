using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Jobs.Get;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.JobFeatures;

public class GetJobEndpoint(IMediator _mediator)
    : Endpoint<JobIdRequest, Results<Ok<RecurringJobDetailResponse>, ProblemHttpResult>>
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
        Results<Ok<RecurringJobDetailResponse>, ProblemHttpResult>
    > ExecuteAsync(JobIdRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetJobQuery(request.JobId), cancellationToken);
        return result.ToOkResult(RecurringJobDetailResponse.From);
    }
}
