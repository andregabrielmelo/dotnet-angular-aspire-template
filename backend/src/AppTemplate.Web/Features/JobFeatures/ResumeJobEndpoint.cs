using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Jobs.Resume;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.JobFeatures;

public class ResumeJobEndpoint(IMediator _mediator)
    : Endpoint<JobIdRequest, Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/admin/jobs/{JobId}/resume");
        Version(ApiVersions.V1);
        Policies(Permission.JobsManage);
        Throttle(hitLimit: JobEndpoints.MutationsPerMinute, durationSeconds: 60);

        Summary(s =>
        {
            s.Summary = "Resume a recurring job";
            s.Description =
                "Scheduled runs happen again. Resuming a job that isn't paused is a no-op.";
            s.Responses[204] = "Resumed";
            s.Responses[400] = "Invalid job id";
            s.Responses[403] = $"Requires the {Permission.JobsManage} permission";
            s.Responses[404] = "No such recurring job";
            s.Responses[429] = "Too many requests";
        });

        Tags(JobEndpoints.Tag);

        // Only a route parameter, no body: don't require a JSON content type.
        Description(builder => builder.ClearDefaultAccepts());
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(
        JobIdRequest request,
        CancellationToken cancellationToken
    ) =>
        (
            await _mediator.Send(new ResumeJobCommand(request.JobId), cancellationToken)
        ).ToNoContentResult();
}
