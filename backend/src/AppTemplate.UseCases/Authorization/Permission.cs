namespace AppTemplate.UseCases.Authorization;

/// <summary>
/// Fine-grained permissions the API enforces. Each is a Keycloak client role on the
/// <c>apptemplate-api</c> client; realm roles (such as <c>admin</c>) bundle them, so role
/// changes happen in Keycloak without code changes. The names are also ASP.NET Core policy
/// names (see the Web project's AuthorizationConfigurations).
/// </summary>
public static class Permission
{
    /// <summary>List users and read any user's profile.</summary>
    public const string UsersRead = "users:read";

    /// <summary>Update any user's profile (everyone may update their own).</summary>
    public const string UsersWrite = "users:write";

    /// <summary>Delete users.</summary>
    public const string UsersDelete = "users:delete";

    /// <summary>List recurring background jobs and their recent runs.</summary>
    public const string JobsRead = "jobs:read";

    /// <summary>Trigger, pause, resume, remove and restore recurring background jobs.</summary>
    public const string JobsManage = "jobs:manage";

    public static IReadOnlyList<string> All { get; } =
    [UsersRead, UsersWrite, UsersDelete, JobsRead, JobsManage];
}
