using System.Security.Cryptography;
using System.Text;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Infrastructure.Data;
using AppTemplate.UseCases.Auth;
using Ardalis.Result;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Identity;

public class IdentityService(
    UserManager<ApplicationUser> _userManager,
    SignInManager<ApplicationUser> _signInManager,
    ApplicationDatabaseContext _db,
    IOptions<JwtConfiguration> _jwtOptions
) : IIdentityService
{
    public async Task<Result<AuthTokensDto>> RegisterAsync(
        User newDomainUser,
        string password,
        CancellationToken cancellationToken
    )
    {
        // EF Core's InMemory provider (used by FunctionalTests) doesn't support transactions -
        // fall back to a manual compensating delete on that provider instead. Checked by
        // provider name (a plain string, needs no reference to the InMemory package) since
        // Infrastructure is production code and the InMemory provider is test-only.
        var isInMemory = _db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
        var transaction = isInMemory
            ? null
            : await _db.Database.BeginTransactionAsync(cancellationToken);

        _db.DomainUsers.Add(newDomainUser);
        await _db.SaveChangesAsync(cancellationToken);

        var applicationUser = new ApplicationUser
        {
            UserName = newDomainUser.Email.Value,
            Email = newDomainUser.Email.Value,
            DomainUserId = newDomainUser.Id,
        };

        var identityResult = await _userManager.CreateAsync(applicationUser, password);
        if (!identityResult.Succeeded)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            else
            {
                _db.DomainUsers.Remove(newDomainUser);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return Result<AuthTokensDto>.Invalid(
                identityResult
                    .Errors.Select(e => new ValidationError(nameof(password), e.Description))
                    .ToArray()
            );
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return await IssueTokensAsync(applicationUser, newDomainUser, cancellationToken);
    }

    public async Task<Result<AuthTokensDto>> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken
    )
    {
        var applicationUser = await _userManager.FindByEmailAsync(email);
        if (applicationUser is null)
        {
            return Result<AuthTokensDto>.Unauthorized();
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(
            applicationUser,
            password,
            lockoutOnFailure: true
        );
        if (!signInResult.Succeeded)
        {
            return Result<AuthTokensDto>.Unauthorized();
        }

        var domainUser = await _db
            .DomainUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == applicationUser.DomainUserId, cancellationToken);
        if (domainUser is null)
        {
            return Result<AuthTokensDto>.Error("User profile not found.");
        }

        return await IssueTokensAsync(applicationUser, domainUser, cancellationToken);
    }

    public async Task<Result<AuthTokensDto>> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken
    )
    {
        var tokenHash = HashToken(refreshToken);
        var existingToken = await _db
            .RefreshTokens.Include(t => t.ApplicationUser)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existingToken?.ApplicationUser is null || !existingToken.IsActive)
        {
            return Result<AuthTokensDto>.Unauthorized();
        }

        var domainUser = await _db
            .DomainUsers.AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Id == existingToken.ApplicationUser.DomainUserId,
                cancellationToken
            );
        if (domainUser is null)
        {
            return Result<AuthTokensDto>.Error("User profile not found.");
        }

        // Rotation: revoke the presented token and issue a fresh pair in the same SaveChanges.
        existingToken.RevokedAtUtc = DateTime.UtcNow;

        return await IssueTokensAsync(existingToken.ApplicationUser, domainUser, cancellationToken);
    }

    public async Task<Result> LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(refreshToken);
        var existingToken = await _db.RefreshTokens.FirstOrDefaultAsync(
            t => t.TokenHash == tokenHash,
            cancellationToken
        );
        if (existingToken is not null && existingToken.RevokedAtUtc is null)
        {
            existingToken.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Idempotent by design: an unknown or already-revoked token still reports success, so
        // this call can never be used to probe whether a given token exists or is still valid.
        return Result.Success();
    }

    private async Task<Result<AuthTokensDto>> IssueTokensAsync(
        ApplicationUser applicationUser,
        User domainUser,
        CancellationToken cancellationToken
    )
    {
        var jwt = _jwtOptions.Value;
        var accessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(jwt.AccessTokenExpirationMinutes);

        var accessToken = JwtBearer.CreateToken(o =>
        {
            o.SigningKey = jwt.SigningKey;
            o.Issuer = jwt.Issuer;
            o.Audience = jwt.Audience;
            o.ExpireAt = accessTokenExpiresAtUtc;
            o.User.Claims.Add(("sub", applicationUser.Id));
            o.User.Claims.Add(("UserId", domainUser.Id.Value.ToString()));
            o.User.Claims.Add(("email", applicationUser.Email ?? String.Empty));
            // Guarantees two tokens issued within the same second still differ (RFC 7519's
            // recommended replay-prevention identifier) - without it, a token issued and then
            // immediately refreshed could be byte-for-byte identical to the one it replaced.
            o.User.Claims.Add(("jti", Guid.NewGuid().ToString()));
        });

        var rawRefreshToken = GenerateRawRefreshToken();
        var refreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(jwt.RefreshTokenExpirationDays);

        _db.RefreshTokens.Add(
            new RefreshToken
            {
                ApplicationUserId = applicationUser.Id,
                TokenHash = HashToken(rawRefreshToken),
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = refreshTokenExpiresAtUtc,
            }
        );
        // Also persists the refresh-token revocation set by RefreshAsync just before calling
        // this method - both writes commit together in one SaveChanges.
        await _db.SaveChangesAsync(cancellationToken);

        return new AuthTokensDto(
            domainUser.Id.Value,
            domainUser.Name.Value,
            applicationUser.Email ?? String.Empty,
            accessToken,
            accessTokenExpiresAtUtc,
            rawRefreshToken,
            refreshTokenExpiresAtUtc
        );
    }

    private static string GenerateRawRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string HashToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
