using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Jobs;
using AppTemplate.UseCases.Jobs.List;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.JobFeatures;

public class ListJobsEndpoint(IMediator _mediator)
    : EndpointWithoutRequest<Results<Ok<IReadOnlyList<RecurringJobResponse>>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("/admin/jobs");
        Version(ApiVersions.V1);
        Policies(Permission.JobsRead);

        Summary(s =>
        {
            s.Summary = "List recurring background jobs";
            s.Description = "Every scheduled recurring job with its schedule, next and last run.";
            s.Responses[200] = "Recurring jobs";
            s.Responses[403] = $"Requires the {Permission.JobsRead} permission";
        });

        Tags(JobEndpoints.Tag);
    }

    public override async Task<
        Results<Ok<IReadOnlyList<RecurringJobResponse>>, ProblemHttpResult>
    > ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListJobsQuery(), cancellationToken);
        return result.ToOkResult<
            IReadOnlyList<RecurringJobDto>,
            IReadOnlyList<RecurringJobResponse>
        >(jobs => jobs.Select(RecurringJobResponse.From).ToList());
    }
}
