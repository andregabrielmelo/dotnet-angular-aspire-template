using AppTemplate.UseCases.Auditing;
using AppTemplate.UseCases.Auditing.List;
using AppTemplate.UseCases.Authorization;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Extensions;
using Microsoft.AspNetCore.WebUtilities;

namespace AppTemplate.Web.Features.AuditFeatures;

public sealed class ListAuditEntriesRequest
{
    public string? EntityType { get; set; }

    public string? EntityKey { get; set; }

    /// <summary><c>user:{sub}</c>, <c>system:{job id}</c>, <c>system:outbox</c> or <c>anonymous</c>.</summary>
    public string? Actor { get; set; }

    /// <summary>Defaults to 7 days before <see cref="To"/>.</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Defaults to now.</summary>
    public DateTimeOffset? To { get; set; }

    public int Page { get; set; } = 1;

    [BindFrom("per_page")]
    public int PerPage { get; set; } = 50;
}

public record AuditEntryListResponse : UseCases.PagedResult<AuditRecord>
{
    public AuditEntryListResponse(
        IReadOnlyList<AuditRecord> Items,
        int Page,
        int PerPage,
        int TotalCount,
        int TotalPages
    )
        : base(Items, Page, PerPage, TotalCount, TotalPages) { }
}

/// <summary>
/// The audit log, newest first. Filters use indexed columns only, always within a time range
/// (7 days unless given), and pages are capped, so a query can't scan the whole log. Never
/// output-cached: entries can be personal and the log keeps growing.
/// </summary>
public sealed class ListAuditEntriesEndpoint(IMediator _mediator)
    : Endpoint<ListAuditEntriesRequest, AuditEntryListResponse, ListAuditEntriesMapper>
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
                "Entity changes and security events, newest first. Filter by entity, actor and time range (default: the last 7 days). Pages are linked in the Link header, keeping the filters.";
            s.ExampleRequest = new ListAuditEntriesRequest
            {
                EntityType = "User",
                Page = 1,
                PerPage = 50,
            };
            s.ResponseExamples[200] = new AuditEntryListResponse(
                [
                    new(
                        Guid.Parse("0193a5c8-7b1e-7c3d-9f00-1a2b3c4d5e6f"),
                        new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero),
                        "user:6f1c9e0a-2b7d-4e3f-8a91-5c0d2e4b7a13",
                        AuditActions.Updated,
                        "User",
                        "1",
                        "succeeded",
                        new Dictionary<string, AuditValueChange>
                        {
                            ["name"] = new("Old Name", "New Name"),
                            ["email"] = new("[redacted]", "[redacted]"),
                        },
                        "4bf92f3577b34da6a3ce929d0e0e4736"
                    ),
                ],
                1,
                50,
                1,
                1
            );

            s.Params["page"] = "1-based page index (default 1)";
            s.Params["per_page"] = $"Page size 1–{ListAuditEntriesQuery.MaxPerPage} (default 50)";

            s.Responses[200] = "A page of audit entries";
            s.Responses[400] = "Invalid filter or page";
            s.Responses[403] = $"Requires the {Permission.AuditRead} permission";
        });

        Tags("Audit");

        Description(builder =>
            builder
                .Accepts<ListAuditEntriesRequest>()
                .Produces<AuditEntryListResponse>(200, "application/json")
                .ProducesProblem(400)
        );
    }

    public override async Task HandleAsync(
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
        if (!result.IsSuccess)
        {
            await Send.ResultAsync(result.ToProblem());
            return;
        }

        var page = result.Value;
        AddLinkHeader(page.Page, page.TotalPages);

        await Send.OkAsync(Map.FromEntity(page), cancellationToken);
    }

    /// <summary>
    /// GitHub-style pagination links, like the users list. Unlike it, the links keep the
    /// request's other query parameters, so following one stays within the same filter.
    /// </summary>
    private void AddLinkHeader(int page, int totalPages)
    {
        var request = HttpContext.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}{request.Path}";
        var query = request
            .Query.Where(parameter => parameter.Key != "page")
            .ToDictionary(parameter => parameter.Key, parameter => (string?)parameter.Value);
        string Link(string rel, int p) =>
            $"<{QueryHelpers.AddQueryString(baseUrl, new Dictionary<string, string?>(query) { ["page"] = p.ToString() })}>; rel=\"{rel}\"";

        var parts = new List<string>();
        if (page > 1)
        {
            parts.Add(Link("first", 1));
            parts.Add(Link("prev", page - 1));
        }
        if (page < totalPages)
        {
            parts.Add(Link("next", page + 1));
            parts.Add(Link("last", totalPages));
        }

        if (parts.Count > 0)
            HttpContext.Response.Headers["Link"] = string.Join(", ", parts);
    }
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

public sealed class ListAuditEntriesMapper
    : Mapper<ListAuditEntriesRequest, AuditEntryListResponse, UseCases.PagedResult<AuditEntryDto>>
{
    public override AuditEntryListResponse FromEntity(UseCases.PagedResult<AuditEntryDto> e)
    {
        var items = e
            .Items.Select(entry => new AuditRecord(
                entry.Id,
                entry.OccurredAtUtc,
                entry.Actor,
                entry.Action,
                entry.EntityType,
                entry.EntityKey,
                entry.Outcome,
                entry.Changes,
                entry.TraceId
            ))
            .ToList();

        return new AuditEntryListResponse(items, e.Page, e.PerPage, e.TotalCount, e.TotalPages);
    }
}
