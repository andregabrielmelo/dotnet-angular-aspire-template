using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Jobs;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.JobFeatures;

public class TriggerJobEndpoint(IJobManagementService _jobs)
    : Endpoint<JobIdRequest, Results<NoContent, NotFound, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/admin/jobs/{JobId}/trigger");
        Policies(Permission.JobsManage);
        Throttle(hitLimit: JobEndpoints.MutationsPerMinute, durationSeconds: 60);

        Summary(s =>
        {
            s.Summary = "Run a recurring job now";
            s.Description =
                "Enqueues an immediate run without changing the schedule; works while paused.";
            s.Responses[204] = "Run enqueued";
            s.Responses[400] = "Invalid job id";
            s.Responses[403] = $"Requires the {Permission.JobsManage} permission";
            s.Responses[404] = "No such recurring job";
            s.Responses[429] = "Too many requests";
        });

        Tags(JobEndpoints.Tag);

        // Only a route parameter, no body: don't require a JSON content type.
        Description(builder => builder.ClearDefaultAccepts());
    }

    public override async Task<Results<NoContent, NotFound, ProblemHttpResult>> ExecuteAsync(
        JobIdRequest request,
        CancellationToken cancellationToken
    ) => (await _jobs.TriggerAsync(request.JobId, cancellationToken)).ToDeleteResult();
}
