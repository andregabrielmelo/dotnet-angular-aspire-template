using AppTemplate.Core.Interfaces;

namespace AppTemplate.FunctionalTests.TestDoubles;

public record RecordedEmail(string To, string From, string Subject, string Body);

/// <summary>
/// Replaces MimeKitEmailSender in functional tests (no real SMTP server available) and lets
/// tests pull the real confirmation/reset token out of a recorded email body, exercising the
/// actual end-to-end flow instead of manipulating the database directly. Registered as a
/// singleton (see AppTemplateWebApplicationFactory) so the same instance is visible both to
/// the request pipeline and to assertions made from the test after the HTTP call returns.
/// </summary>
public class RecordingEmailSender : IEmailSender
{
    public List<RecordedEmail> SentEmails { get; } = [];

    public Task SendEmailAsync(string to, string from, string subject, string body)
    {
        SentEmails.Add(new RecordedEmail(to, from, subject, body));
        return Task.CompletedTask;
    }
}
