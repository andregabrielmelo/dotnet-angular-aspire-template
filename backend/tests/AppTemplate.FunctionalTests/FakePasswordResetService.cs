using System.Collections.Concurrent;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Users.ForgotPassword;
using Ardalis.Result;

namespace AppTemplate.FunctionalTests;

/// <summary>Stands in for Keycloak's Admin API; records who asked for a reset.</summary>
public sealed class FakePasswordResetService : IPasswordResetService
{
    public ConcurrentBag<string> RequestedEmails { get; } = [];

    public Func<EmailAddress, Result> Respond { get; set; } = _ => Result.Success();

    public Task<Result> SendPasswordResetEmailAsync(
        EmailAddress email,
        CancellationToken cancellationToken
    )
    {
        RequestedEmails.Add(email.Value);
        return Task.FromResult(Respond(email));
    }
}
