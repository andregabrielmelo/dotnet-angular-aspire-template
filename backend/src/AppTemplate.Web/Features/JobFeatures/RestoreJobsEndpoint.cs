using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Jobs.Restore;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.JobFeatures;

public class RestoreJobsEndpoint(IMediator _mediator)
    : EndpointWithoutRequest<Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/admin/jobs/restore");
        Version(ApiVersions.V1);
        Policies(Permission.JobsManage);
        Options(x => x.RequireRateLimiting(RateLimitPolicies.JobMutations));

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
        var result = await _mediator.Send(new RestoreJobsCommand(), cancellationToken);
        return result.ToNoContentResult();
    }
}
