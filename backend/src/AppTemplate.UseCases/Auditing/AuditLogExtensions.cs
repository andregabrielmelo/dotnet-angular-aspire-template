using AppTemplate.Core.Enums;
using AppTemplate.Core.Interfaces;

namespace AppTemplate.UseCases.Auditing;

public static class AuditLogExtensions
{
    /// <summary>Records the outcome of a use case result: success, or failure with no detail.</summary>
    public static Task RecordAsync(
        this IAuditLog auditLog,
        string action,
        AuditTarget target,
        Ardalis.Result.IResult result,
        CancellationToken cancellationToken
    ) =>
        auditLog.RecordAsync(
            action,
            target,
            result.IsOk() ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            cancellationToken
        );

    private static bool IsOk(this Ardalis.Result.IResult result) =>
        result.Status is ResultStatus.Ok or ResultStatus.NoContent or ResultStatus.Created;
}
