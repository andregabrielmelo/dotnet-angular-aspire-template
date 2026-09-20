namespace AppTemplate.Core.Aggregates.UserAggregate.Specifications;

public class UserByIdSpecification : Specification<User>
{
    public UserByIdSpecification(UserId personId) => Query.Where(person => person.Id == personId);
}
