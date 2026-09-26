using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Jobs;

namespace AppTemplate.Web.Features.JobFeatures;

public class ListJobsEndpoint(IJobManagementService _jobs)
    : EndpointWithoutRequest<Ok<IReadOnlyList<RecurringJobResponse>>>
{
    public override void Configure()
    {
        Get("/admin/jobs");
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

    public override async Task<Ok<IReadOnlyList<RecurringJobResponse>>> ExecuteAsync(
        CancellationToken cancellationToken
    )
    {
        var jobs = await _jobs.GetRecurringJobsAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<RecurringJobResponse>>(
            jobs.Select(RecurringJobResponse.From).ToList()
        );
    }
}
