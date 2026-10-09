namespace AppTemplate.UseCases.Jobs.List;

/// <summary>Every scheduled recurring job. Not paged: there are only a handful.</summary>
public record ListJobsQuery : IQuery<Result<IReadOnlyList<RecurringJobDto>>>;
