using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Auth;
using AppTemplate.UseCases.Auth.Register;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Auth;

public class RegisterHandlerTests
{
    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();

    private RegisterHandler CreateHandler() => new(_repository, _identityService);

    [Fact]
    public async Task Handle_WithNewEmail_DelegatesToIdentityServiceWithoutPersistingItself()
    {
        _repository
            .FirstOrDefaultAsync(Arg.Any<UserByEmailSpecification>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

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
            .RegisterAsync(Arg.Any<User>(), "Passw0rd!", Arg.Any<CancellationToken>())
            .Returns(Result<AuthTokensDto>.Success(expectedTokens));

        var command = new RegisterCommand(
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com"),
            "Passw0rd!",
            ""
        );

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedTokens, result.Value);
        await _identityService
            .Received(1)
            .RegisterAsync(
                Arg.Is<User>(u =>
                    u.Name.Value == "Ada Lovelace" && u.Email.Value == "ada@example.com"
                ),
                "Passw0rd!",
                Arg.Any<CancellationToken>()
            );
        await _repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithAlreadyRegisteredEmail_ReturnsInvalidWithoutCallingIdentityService()
    {
        var existingUser = User.Create(
            UserName.From("Existing User"),
            new EmailAddress("ada@example.com")
        );
        _repository
            .FirstOrDefaultAsync(Arg.Any<UserByEmailSpecification>(), Arg.Any<CancellationToken>())
            .Returns(existingUser);

        var command = new RegisterCommand(
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com"),
            "Passw0rd!",
            ""
        );

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        await _identityService
            .DidNotReceive()
            .RegisterAsync(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
