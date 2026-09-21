using AppTemplate.UseCases.Auth;
using AppTemplate.UseCases.Auth.Login;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Auth;

public class LoginHandlerTests
{
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();

    [Fact]
    public async Task Handle_DelegatesToIdentityService()
    {
        var expectedTokens = new AuthTokensDto(
            1,
            "Ada Lovelace",
            "ada@example.com",
            "access-token",
            DateTime.UtcNow,
            "refresh-token",
            DateTime.UtcNow
        );
        _identityService
            .LoginAsync("ada@example.com", "Passw0rd!", Arg.Any<CancellationToken>())
            .Returns(Result<AuthTokensDto>.Success(expectedTokens));

        var result = await new LoginHandler(_identityService).Handle(
            new LoginCommand("ada@example.com", "Passw0rd!"),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedTokens, result.Value);
    }

    [Fact]
    public async Task Handle_WithBadCredentials_ReturnsUnauthorized()
    {
        _identityService
            .LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthTokensDto>.Unauthorized());

        var result = await new LoginHandler(_identityService).Handle(
            new LoginCommand("ada@example.com", "wrong-password"),
            CancellationToken.None
        );

        Assert.Equal(ResultStatus.Unauthorized, result.Status);
    }
}
