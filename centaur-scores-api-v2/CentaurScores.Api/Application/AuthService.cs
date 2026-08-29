using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CentaurScores.Api.Application;

public sealed record AuthenticatedAccount(string Token, DateTime ExpiresAt, Account Account);

public interface IAuthService
{
    Task<AuthenticatedAccount?> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<TenantAccessView>> GetAuthorizedTenantsAsync(Guid accountId, CancellationToken cancellationToken);
    Task<AuthenticatedAccount?> SelectTenantAsync(Guid accountId, Guid tenantId, DateTime expiresAtUtc, CancellationToken cancellationToken);
}

public sealed class AuthService(ApplicationDbContext db, IConfiguration configuration) : IAuthService
{
    public async Task<AuthenticatedAccount?> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        // Username is now globally unique (see the Account.Username index), so no tenant is needed to find
        // the account. FirstOrDefault (not Single) so a stale duplicate from before that index was enforced
        // fails safely as "not found" rather than throwing.
        var account = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(item => item.Username == request.Username, cancellationToken);
        if (account is null || !Passwords.Verify(request.Password, account.PasswordHash)) return null;
        var expiresAtUtc = DateTime.UtcNow.AddHours(configuration.GetValue("Jwt:Hours", 4));
        // No tenant has been selected yet - tenant_id is empty until POST /api/auth/select-tenant is called.
        // ApiControllerBase rejects an empty tenant on every tenant-scoped endpoint, so this token can only
        // be used for /api/auth/me and /api/auth/select-tenant until then.
        var token = BuildToken(account, Guid.Empty, expiresAtUtc);
        return new AuthenticatedAccount(token, expiresAtUtc, account);
    }

    public async Task<IReadOnlyList<TenantAccessView>> GetAuthorizedTenantsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == accountId, cancellationToken);
        return account is null ? [] : await ResolveAuthorizedTenantsAsync(account, cancellationToken);
    }

    public async Task<AuthenticatedAccount?> SelectTenantAsync(Guid accountId, Guid tenantId, DateTime expiresAtUtc, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == accountId, cancellationToken);
        if (account is null) return null;
        var authorizedTenants = await ResolveAuthorizedTenantsAsync(account, cancellationToken);
        if (!authorizedTenants.Any(item => item.TenantId == tenantId)) return null;
        // expiresAtUtc is the caller's original token expiry - this re-mints a token for a different
        // tenant, it does not refresh or extend the session.
        var token = BuildToken(account, tenantId, expiresAtUtc);
        return new AuthenticatedAccount(token, expiresAtUtc, account);
    }

    // Walks down Tenant.ParentTenantId from the account's home tenant, collecting the home tenant plus
    // every descendant, all at the account's own unchanged Authorization level. There is no per-tenant
    // grant table - access is derived purely from the existing hierarchy.
    private async Task<IReadOnlyList<TenantAccessView>> ResolveAuthorizedTenantsAsync(Account account, CancellationToken cancellationToken)
    {
        var allTenants = await db.Tenants.AsNoTracking().ToListAsync(cancellationToken);
        var homeTenant = allTenants.SingleOrDefault(item => item.Id == account.TenantId);
        if (homeTenant is null) return [];
        var childrenByParent = allTenants.Where(item => item.ParentTenantId is not null).ToLookup(item => item.ParentTenantId!.Value);
        var result = new List<TenantAccessView>();
        var visited = new HashSet<Guid>();
        var queue = new Queue<Tenant>();
        queue.Enqueue(homeTenant);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current.Id)) continue;
            result.Add(new TenantAccessView(current.Id, current.Name, current.LogoUrl, account.Authorization.ToString()));
            foreach (var child in childrenByParent[current.Id]) queue.Enqueue(child);
        }
        return result;
    }

    private string BuildToken(Account account, Guid tenantId, DateTime expiresAtUtc)
    {
        var secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is required");
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()), new Claim("tenant_id", tenantId.ToString()), new Claim(ClaimTypes.Name, account.DisplayName ?? account.Username), new Claim(ClaimTypes.Email, account.Email ?? ""), new Claim(ClaimTypes.Role, account.Authorization.ToString()) };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: claims, expires: expiresAtUtc, signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public static class Passwords
{
    public static string GenerateRandom() => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(18));

    public static string Hash(string value)
    {
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var hash = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(value, salt, 120_000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256$120000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string value, string encoded)
    {
        var parts = encoded.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out var iterations)) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(value, salt, iterations, System.Security.Cryptography.HashAlgorithmName.SHA256, expected.Length);
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}