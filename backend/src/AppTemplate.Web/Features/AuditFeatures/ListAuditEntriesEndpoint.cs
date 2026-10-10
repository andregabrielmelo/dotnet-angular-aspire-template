using AppTemplate.UseCases.Auditing;
using AppTemplate.UseCases.Auditing.List;
using AppTemplate.UseCases.Authorization;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Extensions;
using PagedResult = AppTemplate.UseCases.PagedResult<AppTemplate.UseCases.Auditing.AuditEntryDto>;

namespace AppTemplate.Web.Features.AuditFeatures;

public sealed class ListAuditEntriesRequest
{
    public string? EntityType { get; set; }

    public string? EntityKey { get; set; }

    /// <summary><c>user:{sub}</c>, <c>system:{job id}</c> or <c>anonymous</c>.</summary>
    public string? Actor { get; set; }

    /// <summary>Defaults to 7 days before <see cref="To"/>.</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Defaults to now.</summary>
    public DateTimeOffset? To { get; set; }

    public int Page { get; set; } = 1;

    [BindFrom("per_page")]
    public int PerPage { get; set; } = 50;
}

public sealed class ListAuditEntriesValidator : Validator<ListAuditEntriesRequest>
{
    public ListAuditEntriesValidator()
    {
        RuleFor(request => request.Page).GreaterThanOrEqualTo(1);
        RuleFor(request => request.PerPage).InclusiveBetween(1, ListAuditEntriesQuery.MaxPerPage);
        RuleFor(request => request.From)
            .LessThanOrEqualTo(request => request.To)
            .When(request => request.From is not null && request.To is not null)
            .WithMessage("'from' must not be after 'to'.");
    }
}

/// <summary>
/// The audit log, newest first. Filters use indexed columns only, always within a time range
/// (7 days unless given), and pages are capped, so a query can't scan the whole log. Never
/// output-cached: entries can be personal and the log keeps growing.
/// </summary>
public sealed class ListAuditEntriesEndpoint(IMediator _mediator)
    : Endpoint<ListAuditEntriesRequest, Results<Ok<PagedResult>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("/admin/audit");
        Version(ApiVersions.V1);
        Policies(Permission.AuditRead);

        Summary(s =>
        {
            s.Summary = "List audit entries";
            s.Description =
                "Entity changes and security events, newest first. Filter by entity, actor and time range (default: the last 7 days).";
            s.Responses[200] = "A page of audit entries";
            s.Responses[400] = "Invalid filter or page";
            s.Responses[403] = $"Requires the {Permission.AuditRead} permission";
        });

        Tags("Audit");
    }

    public override async Task<Results<Ok<PagedResult>, ProblemHttpResult>> ExecuteAsync(
        ListAuditEntriesRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new ListAuditEntriesQuery(
                request.EntityType,
                request.EntityKey,
                request.Actor,
                request.From,
                request.To,
                request.Page,
                request.PerPage
            ),
            cancellationToken
        );
        return result.ToOkResult(page => page);
    }
}
