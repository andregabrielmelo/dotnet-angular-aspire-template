using AppTemplate.UseCases.Users.ForgotPassword;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Users;

public class ForgotPasswordHandlerTests
{
    private readonly IPasswordResetService _passwordResetService =
        Substitute.For<IPasswordResetService>();

    private static readonly ForgotPasswordCommand Command = new(
        new EmailAddress("ada@example.com")
    );

    private Task<Result> Handle() =>
        new ForgotPasswordHandler(_passwordResetService)
            .Handle(Command, CancellationToken.None)
            .AsTask();

    [Fact]
    public async Task Handle_WhenEmailIsSent_ReturnsSuccess()
    {
        _passwordResetService
            .SendPasswordResetEmailAsync(Command.Email, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await Handle();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_WithUnknownEmail_StillReturnsSuccess()
    {
        _passwordResetService
            .SendPasswordResetEmailAsync(Command.Email, Arg.Any<CancellationToken>())
            .Returns(Result.NotFound());

        var result = await Handle();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_WhenProviderIsUnavailable_PassesTheFailureThrough()
    {
        _passwordResetService
            .SendPasswordResetEmailAsync(Command.Email, Arg.Any<CancellationToken>())
            .Returns(Result.Unavailable("down"));

        var result = await Handle();

        Assert.Equal(ResultStatus.Unavailable, result.Status);
    }
}
