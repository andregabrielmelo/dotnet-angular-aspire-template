namespace AppTemplate.UseCases.Users.List;

public record ListUsersQuery(int? Page = 1, int? PerPage = Constants.DEFAULT_PAGE_SIZE)
    : IQuery<Result<PagedResult<UserDto>>>;

public class ListUsersHandler(IListUsersQueryService query)
    : IQueryHandler<ListUsersQuery, Result<PagedResult<UserDto>>>
{
    private readonly IListUsersQueryService _query = query;

    public async ValueTask<Result<PagedResult<UserDto>>> Handle(
        ListUsersQuery request,
        CancellationToken cancellationToken
    )
    {
        var result = await _query.ListAsync(
            request.Page ?? 1,
            request.PerPage ?? Constants.DEFAULT_PAGE_SIZE
        );

        return Result.Success(result);
    }
}
