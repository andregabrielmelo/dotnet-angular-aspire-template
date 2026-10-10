using AppTemplate.UseCases.Auditing;
using AppTemplate.UseCases.Behaviors;
using AppTemplate.UseCases.Jobs.Pause;
using AppTemplate.UseCases.Jobs.Remove;
using AppTemplate.UseCases.Jobs.Restore;
using AppTemplate.UseCases.Jobs.Resume;
using AppTemplate.UseCases.Jobs.Trigger;
using AppTemplate.UseCases.Outbox.RequeueDeadLettered;
using Ardalis.Result;
using Mediator;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AppTemplate.UnitTests.UseCases.Behaviors;

public class AuditingBehaviorTests
{
    private static readonly PauseJobCommand Command = new("sync-user-profiles");

    private readonly IAuditLog _audit = Substitute.For<IAuditLog>();

    private AuditingBehavior<PauseJobCommand, Result> Behavior =>
        new(_audit, NullLogger<AuditingBehavior<PauseJobCommand, Result>>.Instance);

    private static MessageHandlerDelegate<PauseJobCommand, Result> Returns(Result result) =>
        (_, _) => ValueTask.FromResult(result);

    [Theory]
    [InlineData(ResultStatus.Ok, AuditOutcome.Succeeded)]
    [InlineData(ResultStatus.NotFound, AuditOutcome.Failed)]
    [InlineData(ResultStatus.Unavailable, AuditOutcome.Failed)]
    [InlineData(ResultStatus.Forbidden, AuditOutcome.Failed)]
    public async Task ReturnedResult_IsRecordedWithItsOutcome(
        ResultStatus status,
        AuditOutcome outcome
    )
    {
        var result = status switch
        {
            ResultStatus.Ok => Result.Success(),
            ResultStatus.NotFound => Result.NotFound(),
            ResultStatus.Unavailable => Result.Unavailable(),
            _ => Result.Forbidden(),
        };

        var response = await Behavior.Handle(Command, Returns(result), CancellationToken.None);

        Assert.Same(result, response);
        await _audit
            .Received(1)
            .RecordAsync(
                AuditActions.JobPaused,
                AuditTarget.Job(Command.JobId),
                outcome,
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ThrowingCommand_IsRecordedAsFailed_AndTheExceptionPropagatesUnchanged()
    {
        var thrown = new InvalidOperationException("boom");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Behavior.Handle(Command, (_, _) => throw thrown, CancellationToken.None)
        );

        Assert.Same(thrown, exception);
        await _audit
            .Received(1)
            .RecordAsync(
                AuditActions.JobPaused,
                AuditTarget.Job(Command.JobId),
                AuditOutcome.Failed,
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task AuditWriteFailure_FailsAnOtherwiseSuccessfulCommand()
    {
        _audit
            .RecordAsync(
                Arg.Any<string>(),
                Arg.Any<AuditTarget>(),
                Arg.Any<AuditOutcome>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new TimeoutException("database down"));

        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await Behavior.Handle(Command, Returns(Result.Success()), CancellationToken.None)
        );
    }

    [Fact]
    public async Task AuditWriteFailure_AfterTheCommandThrew_KeepsTheCommandsException()
    {
        _audit
            .RecordAsync(
                Arg.Any<string>(),
                Arg.Any<AuditTarget>(),
                Arg.Any<AuditOutcome>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new TimeoutException("database down"));
        var thrown = new InvalidOperationException("boom");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Behavior.Handle(Command, (_, _) => throw thrown, CancellationToken.None)
        );

        Assert.Same(thrown, exception);
    }

    public static TheoryData<IAuditedCommand, string, AuditTarget> AuditedCommands() =>
        new()
        {
            { new TriggerJobCommand("j"), AuditActions.JobTriggered, AuditTarget.Job("j") },
            { new PauseJobCommand("j"), AuditActions.JobPaused, AuditTarget.Job("j") },
            { new ResumeJobCommand("j"), AuditActions.JobResumed, AuditTarget.Job("j") },
            { new RemoveJobCommand("j"), AuditActions.JobRemoved, AuditTarget.Job("j") },
            { new RestoreJobsCommand(), AuditActions.JobsRestored, new AuditTarget("job", "*") },
            {
                new RequeueDeadLetteredMessagesCommand(),
                AuditActions.OutboxDeadLettersRequeued,
                AuditTarget.Outbox()
            },
        };

    [Theory]
    [MemberData(nameof(AuditedCommands))]
    public void AuditedCommand_NamesItsActionAndTarget(
        IAuditedCommand command,
        string action,
        AuditTarget target
    )
    {
        Assert.Equal(action, command.AuditAction);
        Assert.Equal(target, command.AuditTarget);
    }
}
