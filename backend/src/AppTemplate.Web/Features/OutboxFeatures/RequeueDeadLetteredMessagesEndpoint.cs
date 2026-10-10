using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Outbox.RequeueDeadLettered;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Extensions;

namespace AppTemplate.Web.Features.OutboxFeatures;

public sealed record RequeueDeadLetteredMessagesResponse(int Requeued);

/// <summary>
/// Retries every dead-lettered outbox message, once whatever made them fail (a bug, a long
/// outage) is fixed. Messages go back into the queue with a fresh attempt count.
/// </summary>
public sealed class RequeueDeadLetteredMessagesEndpoint(IMediator _mediator)
    : EndpointWithoutRequest<Results<Ok<RequeueDeadLetteredMessagesResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/admin/outbox/dead-letters/requeue");
        Version(ApiVersions.V1);
        Policies(Permission.JobsManage);
        Options(x => x.RequireRateLimiting(RateLimitPolicies.JobMutations));

        Summary(s =>
        {
            s.Summary = "Requeue dead-lettered outbox messages";
            s.Description =
                "Makes every dead-lettered outbox message due again with a fresh attempt count, and returns how many.";
            s.Responses[200] = "Messages requeued";
            s.Responses[403] = $"Requires the {Permission.JobsManage} permission";
            s.Responses[429] = "Too many requests";
        });

        Tags("Outbox");
    }

    public override async Task<
        Results<Ok<RequeueDeadLetteredMessagesResponse>, ProblemHttpResult>
    > ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new RequeueDeadLetteredMessagesCommand(),
            cancellationToken
        );
        return result.ToOkResult(requeued => new RequeueDeadLetteredMessagesResponse(requeued));
    }
}
