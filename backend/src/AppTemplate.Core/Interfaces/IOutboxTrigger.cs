namespace AppTemplate.Core.Interfaces;

/// <summary>
/// Starts a relay run soon after messages are written (the fast path).
/// Implemented by <c>HangfireOutboxTrigger</c>. Used by the outbox interceptor once messages are saved.
/// </summary>
public interface IOutboxTrigger
{
    void MessagesWritten();
}
