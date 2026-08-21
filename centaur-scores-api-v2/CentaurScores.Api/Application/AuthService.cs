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
}

public sealed class AuthService(ApplicationDbContext db, IConfiguration configuration) : IAuthService
{
    public async Task<AuthenticatedAccount?> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(item => item.TenantId == request.TenantId && item.Username == request.Username, cancellationToken);
        if (account is null || !Passwords.Verify(request.Password, account.PasswordHash)) return null;
        var secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is required");
        var expires = DateTime.UtcNow.AddHours(configuration.GetValue("Jwt:Hours", 4));
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()), new Claim("tenant_id", account.TenantId.ToString()), new Claim(ClaimTypes.Name, account.DisplayName ?? account.Username), new Claim(ClaimTypes.Email, account.Email ?? ""), new Claim(ClaimTypes.Role, account.Authorization.ToString()) };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: claims, expires: expires, signingCredentials: credentials);
        return new AuthenticatedAccount(new JwtSecurityTokenHandler().WriteToken(token), expires, account);
    }
}

public static class Passwords
{
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