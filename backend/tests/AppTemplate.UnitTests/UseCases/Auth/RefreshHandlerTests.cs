using AppTemplate.UseCases.Auth;
using AppTemplate.UseCases.Auth.Refresh;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Auth;

public class RefreshHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToIdentityService()
    {
        var identityService = Substitute.For<IIdentityService>();
        var expectedTokens = new AuthTokensDto(
            1,
            "Ada Lovelace",
            "ada@example.com",
            "new-access-token",
            DateTime.UtcNow,
            "new-refresh-token",
            DateTime.UtcNow
        );
        identityService
            .RefreshAsync("old-refresh-token", Arg.Any<CancellationToken>())
            .Returns(Result<AuthTokensDto>.Success(expectedTokens));

        var result = await new RefreshHandler(identityService).Handle(
            new RefreshCommand("old-refresh-token"),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedTokens, result.Value);
    }

    [Fact]
    public async Task Handle_WithRevokedToken_ReturnsUnauthorized()
    {
        var identityService = Substitute.For<IIdentityService>();
        identityService
            .RefreshAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<AuthTokensDto>.Unauthorized());

        var result = await new RefreshHandler(identityService).Handle(
            new RefreshCommand("revoked-token"),
            CancellationToken.None
        );

        Assert.Equal(ResultStatus.Unauthorized, result.Status);
    }
}
