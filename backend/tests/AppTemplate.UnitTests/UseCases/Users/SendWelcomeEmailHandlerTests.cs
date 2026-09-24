using AppTemplate.Core.Interfaces;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Users.SendWelcomeEmail;
using Ardalis.Result;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AppTemplate.UnitTests.UseCases.Users;

public class SendWelcomeEmailHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly User _user = User.Create(
        "sub-1",
        UserName.From("Ada Lovelace"),
        new EmailAddress("ada@example.com")
    );

    private static readonly SendWelcomeEmailCommand Command = new(UserId.From(1));

    public SendWelcomeEmailHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _repository.GetByIdAsync(Command.UserId, Arg.Any<CancellationToken>()).Returns(_user);
    }

    private Task<Result> Handle() =>
        new SendWelcomeEmailHandler(
            _repository,
            _emailSender,
            Options.Create(new WelcomeEmailOptions { From = "hello@example.com" }),
            _timeProvider
        )
            .Handle(Command, CancellationToken.None)
            .AsTask();

    [Fact]
    public async Task Handle_FirstTime_SendsAndRecordsTheEmail()
    {
        var result = await Handle();

        Assert.True(result.IsSuccess);
        await _emailSender
            .Received(1)
            .SendEmailAsync(
                "ada@example.com",
                "hello@example.com",
                Arg.Any<string>(),
                Arg.Is<string>(body => body.Contains("Ada Lovelace")),
                Arg.Any<CancellationToken>()
            );
        Assert.Equal(Now, _user.WelcomeEmailSentAtUtc);
        await _repository.Received(1).UpdateAsync(_user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadySent_DoesNothing()
    {
        _user.MarkWelcomeEmailSent(Now.AddDays(-1));

        var result = await Handle();

        Assert.True(result.IsSuccess);
        await _emailSender
            .DidNotReceiveWithAnyArgs()
            .SendEmailAsync(default!, default!, default!, default!, default);
        await _repository
            .DidNotReceive()
            .UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSendingFails_LeavesTheMarkerUnsetSoARetrySendsAgain()
    {
        _emailSender
            .SendEmailAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        await Assert.ThrowsAsync<InvalidOperationException>(Handle);

        Assert.Null(_user.WelcomeEmailSentAtUtc);
        await _repository
            .DidNotReceive()
            .UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MissingUser_ReturnsNotFound()
    {
        _repository.GetByIdAsync(Command.UserId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await Handle();

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }
}
