using AppTemplate.UseCases.Auditing;
using Microsoft.Extensions.Logging;

namespace AppTemplate.UseCases.Behaviors;

/// <summary>
/// Records every <see cref="IAuditedCommand"/> in the audit log, so handlers don't have to:
/// <list type="bullet">
/// <item>a returned <c>Result</c> is recorded as succeeded (Ok, Created, NoContent) or failed;</item>
/// <item>a thrown exception is recorded as failed and rethrown unchanged.</item>
/// </list>
/// Auditing is mandatory: when the record of a returned result can't be written, the command
/// fails. When the command already threw, its exception wins and the audit failure is logged.
/// Requests refused by authorization never reach Mediator; Web audits those
/// (<c>AuditingAuthorizationResultHandler</c>).
/// </summary>
public sealed partial class AuditingBehavior<TMessage, TResponse>(
    IAuditLog auditLog,
    ILogger<AuditingBehavior<TMessage, TResponse>> logger
) : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage, IAuditedCommand
    where TResponse : IResult
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken
    )
    {
        TResponse response;
        try
        {
            response = await next(message, cancellationToken);
        }
        catch (Exception exception)
        {
            await RecordFailureOfAsync(message, exception);
            throw;
        }

        await auditLog.RecordAsync(
            message.AuditAction,
            message.AuditTarget,
            IsSuccess(response) ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            cancellationToken
        );
        return response;
    }

    private async Task RecordFailureOfAsync(TMessage message, Exception exception)
    {
        try
        {
            // Not the request's token: a cancelled request is still a failed action to record.
            await auditLog.RecordAsync(
                message.AuditAction,
                message.AuditTarget,
                AuditOutcome.Failed,
                CancellationToken.None
            );
        }
        catch (Exception auditFailure)
        {
            LogAuditFailed(logger, auditFailure, message.AuditAction, exception.GetType().Name);
        }
    }

    private static bool IsSuccess(TResponse response) =>
        response.Status is ResultStatus.Ok or ResultStatus.Created or ResultStatus.NoContent;

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Couldn't audit the failure of {AuditAction}, which threw {ExceptionType}"
    )]
    private static partial void LogAuditFailed(
        ILogger logger,
        Exception exception,
        string auditAction,
        string exceptionType
    );
}
