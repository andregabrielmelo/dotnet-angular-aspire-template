namespace AppTemplate.UseCases.Users.List;

public record ListUsersQuery(int? Page = 1, int? PerPage = Constants.DEFAULT_PAGE_SIZE)
    : IQuery<Result<PagedResult<UserDto>>>;
