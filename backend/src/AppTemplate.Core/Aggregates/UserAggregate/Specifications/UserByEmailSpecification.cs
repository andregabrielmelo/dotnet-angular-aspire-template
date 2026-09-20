using AppTemplate.Core.ValueObjects;

namespace AppTemplate.Core.Aggregates.UserAggregate.Specifications;

public class UserByEmailSpecification : Specification<User>
{
    public UserByEmailSpecification(EmailAddress email) => Query.Where(user => user.Email == email);
}
