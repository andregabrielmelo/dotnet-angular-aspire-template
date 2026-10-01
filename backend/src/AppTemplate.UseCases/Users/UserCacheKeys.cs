using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Users;

/// <summary>
/// Cache keys for the User feature, following <c>{feature}:{resource}:{identifier}</c>.
/// </summary>
public static class UserCacheKeys
{
    public static string ById(UserId id) => $"users:id:{id.Value}";
}
