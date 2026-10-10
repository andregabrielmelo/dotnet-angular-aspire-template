using AppTemplate.UseCases.Auditing;
using AppTemplate.UseCases.Jobs;
using AppTemplate.UseCases.Jobs.Pause;
using AppTemplate.UseCases.Jobs.Remove;
using AppTemplate.UseCases.Jobs.Restore;
using AppTemplate.UseCases.Jobs.Resume;
using AppTemplate.UseCases.Jobs.Trigger;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Jobs;

public class JobCommandHandlerTests
{
    private const string JobId = "sync-user-profiles";

    private readonly IJobManagementService _jobs = Substitute.For<IJobManagementService>();
    private readonly IAuditLog _audit = Substitute.For<IAuditLog>();

    [Fact]
    public async Task Trigger_PassesTheServiceResultThrough()
    {
        _jobs.TriggerAsync(JobId, Arg.Any<CancellationToken>()).Returns(Result.NotFound());

        var result = await new TriggerJobHandler(_jobs, _audit).Handle(
            new TriggerJobCommand(JobId),
            CancellationToken.None
        );

        Assert.Equal(ResultStatus.NotFound, result.Status);
        await _audit
            .Received(1)
            .RecordAsync(
                AuditActions.JobTriggered,
                AuditTarget.Job(JobId),
                AuditOutcome.Failed,
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Pause_PassesTheServiceResultThrough()
    {
        _jobs.PauseAsync(JobId, Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await new PauseJobHandler(_jobs, _audit).Handle(
            new PauseJobCommand(JobId),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        await _audit
            .Received(1)
            .RecordAsync(
                AuditActions.JobPaused,
                AuditTarget.Job(JobId),
                AuditOutcome.Succeeded,
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Resume_PassesTheServiceResultThrough()
    {
        _jobs.ResumeAsync(JobId, Arg.Any<CancellationToken>()).Returns(Result.NotFound());

        var result = await new ResumeJobHandler(_jobs, _audit).Handle(
            new ResumeJobCommand(JobId),
            CancellationToken.None
        );

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Remove_PassesTheServiceResultThrough()
    {
        _jobs.RemoveAsync(JobId, Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await new RemoveJobHandler(_jobs, _audit).Handle(
            new RemoveJobCommand(JobId),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Restore_PassesTheCancellationTokenThrough()
    {
        using var cancellation = new CancellationTokenSource();
        _jobs.RestoreAsync(cancellation.Token).Returns(Result.Unavailable());

        var result = await new RestoreJobsHandler(_jobs, _audit).Handle(
            new RestoreJobsCommand(),
            cancellation.Token
        );

        Assert.Equal(ResultStatus.Unavailable, result.Status);
    }
}
