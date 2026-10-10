using AppTemplate.UseCases.Authorization;

namespace AppTemplate.Infrastructure.Auditing;

/// <summary>
/// Who the current scope acts for, for the audit log. Background work sets
/// <see cref="SystemActor"/> (the recurring job runner sets <c>system:{job id}</c>, the outbox
/// relay <c>system:outbox</c>); otherwise it's the signed-in user, or <c>anonymous</c>.
/// </summary>
public sealed class AuditActorContext(IServiceProvider services)
{
    public const string Anonymous = "anonymous";

    public string? SystemActor { get; set; }

    public string Actor =>
        SystemActor
        ?? (
            services.GetService<ICurrentUser>()?.ExternalId is { Length: > 0 } subject
                ? $"user:{subject}"
                : Anonymous
        );
}
