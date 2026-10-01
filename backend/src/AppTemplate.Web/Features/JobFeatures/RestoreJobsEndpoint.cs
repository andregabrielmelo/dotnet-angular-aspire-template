using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Jobs;

namespace AppTemplate.Web.Features.JobFeatures;

public class RestoreJobsEndpoint(IJobManagementService _jobs)
    : EndpointWithoutRequest<Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/admin/jobs/restore");
        Policies(Permission.JobsManage);
        Throttle(hitLimit: JobEndpoints.MutationsPerMinute, durationSeconds: 60);

        Summary(s =>
        {
            s.Summary = "Restore all recurring jobs";
            s.Description =
                "Re-registers every job definition (e.g. after removing one), keeping paused jobs paused.";
            s.Responses[204] = "Jobs restored";
            s.Responses[403] = $"Requires the {Permission.JobsManage} permission";
            s.Responses[429] = "Too many requests";
            s.Responses[503] = "Job scheduling is disabled";
        });

        Tags(JobEndpoints.Tag);
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(
        CancellationToken cancellationToken
    )
    {
        var result = await _jobs.RestoreAsync(cancellationToken);
        return result.IsSuccess
            ? TypedResults.NoContent()
            : TypedResults.Problem(
                title: "Restore failed",
                detail: string.Join("; ", result.Errors),
                statusCode: result.Status == ResultStatus.Unavailable
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status500InternalServerError
            );
    }
}
