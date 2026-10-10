using AppTemplate.UseCases.Jobs.Get;
using AppTemplate.UseCases.Jobs.List;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Jobs;

public class JobQueryHandlerTests
{
    private readonly IJobManagementService _jobs = Substitute.For<IJobManagementService>();

    private static readonly RecurringJobDto Job = new(
        "sync-user-profiles",
        "0 * * * *",
        NextExecution: null,
        LastExecution: null,
        LastStatus: null,
        IsPaused: false,
        CreatedAt: null
    );

    [Fact]
    public async Task GetJob_ReturnsTheJobWithItsRecentRuns()
    {
        var detail = new RecurringJobDetailDto(Job, []);
        _jobs.GetRecurringJobAsync(Job.Id, Arg.Any<CancellationToken>()).Returns(detail);

        var result = await new GetJobHandler(_jobs).Handle(
            new GetJobQuery(Job.Id),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Same(detail, result.Value);
    }

    [Fact]
    public async Task GetJob_UnknownId_ReturnsNotFound()
    {
        _jobs
            .GetRecurringJobAsync("missing", Arg.Any<CancellationToken>())
            .Returns(Result<RecurringJobDetailDto>.NotFound());

        var result = await new GetJobHandler(_jobs).Handle(
            new GetJobQuery("missing"),
            CancellationToken.None
        );

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ListJobs_ReturnsEveryRecurringJob()
    {
        _jobs.GetRecurringJobsAsync(Arg.Any<CancellationToken>()).Returns([Job]);

        var result = await new ListJobsHandler(_jobs).Handle(
            new ListJobsQuery(),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal([Job], result.Value);
    }
}
