using AppTemplate.UseCases.Auth;
using AppTemplate.UseCases.Auth.Logout;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Auth;

public class LogoutHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToIdentityService()
    {
        var identityService = Substitute.For<IIdentityService>();
        identityService
            .LogoutAsync("some-refresh-token", Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await new LogoutHandler(identityService).Handle(
            new LogoutCommand("some-refresh-token"),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        await identityService
            .Received(1)
            .LogoutAsync("some-refresh-token", Arg.Any<CancellationToken>());
    }
}
