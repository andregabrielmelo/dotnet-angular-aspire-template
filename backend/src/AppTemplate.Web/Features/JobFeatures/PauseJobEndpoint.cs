using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Jobs;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.JobFeatures;

public class PauseJobEndpoint(IJobManagementService _jobs)
    : Endpoint<JobIdRequest, Results<NoContent, NotFound, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/admin/jobs/{JobId}/pause");
        Policies(Permission.JobsManage);
        Throttle(hitLimit: JobEndpoints.MutationsPerMinute, durationSeconds: 60);

        Summary(s =>
        {
            s.Summary = "Pause a recurring job";
            s.Description =
                "Stops the schedule until resumed. Survives restarts; pausing twice is a no-op.";
            s.Responses[204] = "Paused";
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
    ) => (await _jobs.PauseAsync(request.JobId, cancellationToken)).ToDeleteResult();
}
