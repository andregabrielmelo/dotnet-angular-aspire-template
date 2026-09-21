using AppTemplate.Core.Aggregates.UserAggregate;
using Microsoft.AspNetCore.Identity;

namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// The Identity-owned credential record (email/password hash/lockout/etc.). Deliberately
/// separate from the domain <see cref="User"/> aggregate in Core - Core must stay framework-free
/// (see ADR-001), and this class exists purely so ASP.NET Core Identity has somewhere to persist
/// login state. <see cref="IdentityUser.UserName"/>/<see cref="IdentityUser.Email"/> are set to
/// the account's email address at registration time; they are Identity's login handle, not the
/// domain user's display name (that's <see cref="User.Name"/>, reached via <see cref="DomainUserId"/>).
/// </summary>
public class ApplicationUser : IdentityUser
{
    public UserId DomainUserId { get; set; }
}
