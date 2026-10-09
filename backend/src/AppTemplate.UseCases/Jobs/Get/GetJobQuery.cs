namespace AppTemplate.UseCases.Jobs.Get;

/// <returns>The recurring job with its recent runs, or NotFound.</returns>
public record GetJobQuery(string JobId) : IQuery<Result<RecurringJobDetailDto>>;
