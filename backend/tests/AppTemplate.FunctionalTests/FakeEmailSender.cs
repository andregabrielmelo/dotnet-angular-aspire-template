using System.Collections.Concurrent;
using AppTemplate.Core.Interfaces;

namespace AppTemplate.FunctionalTests;

public sealed record SentEmail(string To, string From, string Subject, string Body);

public sealed class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<SentEmail> Sent { get; } = new();

    public Task SendEmailAsync(
        string to,
        string from,
        string subject,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        Sent.Enqueue(new SentEmail(to, from, subject, body));
        return Task.CompletedTask;
    }
}
