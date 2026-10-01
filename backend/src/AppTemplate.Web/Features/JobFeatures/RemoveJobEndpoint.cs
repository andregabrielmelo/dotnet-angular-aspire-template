using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Jobs;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.JobFeatures;

public class RemoveJobEndpoint(IJobManagementService _jobs)
    : Endpoint<JobIdRequest, Results<NoContent, NotFound, ProblemHttpResult>>
{
    public override void Configure()
    {
        Delete("/admin/jobs/{JobId}");
        Policies(Permission.JobsManage);
        Throttle(hitLimit: JobEndpoints.MutationsPerMinute, durationSeconds: 60);

        Summary(s =>
        {
            s.Summary = "Remove a recurring job";
            s.Description =
                "Removes the job from the scheduler until the next restore or restart (definitions live in code).";
            s.Responses[204] = "Removed";
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
    ) => (await _jobs.RemoveAsync(request.JobId, cancellationToken)).ToDeleteResult();
}
