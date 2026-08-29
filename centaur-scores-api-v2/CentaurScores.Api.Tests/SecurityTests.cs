using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CentaurScores.Api.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void Password_hash_round_trip_succeeds_and_wrong_password_fails()
    {
        var hash = Passwords.Hash("correct horse");
        Assert.True(Passwords.Verify("correct horse", hash));
        Assert.False(Passwords.Verify("wrong horse", hash));
        Assert.False(Passwords.Verify("correct horse", "not-a-valid-hash"));
    }

    [Fact]
    public async Task Login_issues_a_token_with_an_empty_tenant_claim_and_the_account_own_role()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var root = new Tenant { Id = Guid.NewGuid(), Name = "Root" };
        var account = new Account { Id = Guid.NewGuid(), TenantId = root.Id, Username = "manager", PasswordHash = Passwords.Hash("password"), Authorization = AuthorizationProfile.Manager };
        fixture.Db.Tenants.Add(root);
        fixture.Db.Accounts.Add(account);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.AuthenticateAsync(new LoginRequest("manager", "password"), CancellationToken.None);

        Assert.NotNull(result);
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(result.Token).Claims.ToList();
        Assert.Equal(Guid.Empty.ToString(), claims.Single(item => item.Type == "tenant_id").Value);
        Assert.Equal(AuthorizationProfile.Manager.ToString(), claims.Single(item => item.Type == ClaimTypes.Role).Value);
        Assert.Equal(account.Id, result.Account.Id);
    }

    [Fact]
    public async Task GetAuthorizedTenants_includes_home_tenant_and_all_descendants_at_the_account_own_level()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var root = new Tenant { Id = Guid.NewGuid(), Name = "Root" };
        var child = new Tenant { Id = Guid.NewGuid(), Name = "Child", ParentTenantId = root.Id };
        var grandchild = new Tenant { Id = Guid.NewGuid(), Name = "Grandchild", ParentTenantId = child.Id };
        var account = new Account { Id = Guid.NewGuid(), TenantId = root.Id, Username = "manager", PasswordHash = Passwords.Hash("password"), Authorization = AuthorizationProfile.Manager };
        fixture.Db.Tenants.AddRange(root, child, grandchild);
        fixture.Db.Accounts.Add(account);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.GetAuthorizedTenantsAsync(account.Id, CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.All(result, item => Assert.Equal(AuthorizationProfile.Manager.ToString(), item.Authorization));
        Assert.Contains(result, item => item.TenantId == root.Id);
        Assert.Contains(result, item => item.TenantId == child.Id);
        Assert.Contains(result, item => item.TenantId == grandchild.Id);
    }

    [Fact]
    public async Task GetAuthorizedTenants_excludes_ancestors_and_unrelated_tenants()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var root = new Tenant { Id = Guid.NewGuid(), Name = "Root" };
        var child = new Tenant { Id = Guid.NewGuid(), Name = "Child", ParentTenantId = root.Id };
        var sibling = new Tenant { Id = Guid.NewGuid(), Name = "Sibling", ParentTenantId = root.Id };
        var account = new Account { Id = Guid.NewGuid(), TenantId = child.Id, Username = "viewer", PasswordHash = Passwords.Hash("password"), Authorization = AuthorizationProfile.Viewer };
        fixture.Db.Tenants.AddRange(root, child, sibling);
        fixture.Db.Accounts.Add(account);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.GetAuthorizedTenantsAsync(account.Id, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(child.Id, result[0].TenantId);
    }

    [Fact]
    public async Task SelectTenant_succeeds_for_an_authorized_descendant_and_preserves_the_original_expiry()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var root = new Tenant { Id = Guid.NewGuid(), Name = "Root" };
        var child = new Tenant { Id = Guid.NewGuid(), Name = "Child", ParentTenantId = root.Id };
        var account = new Account { Id = Guid.NewGuid(), TenantId = root.Id, Username = "viewer", PasswordHash = Passwords.Hash("password"), Authorization = AuthorizationProfile.Viewer };
        fixture.Db.Tenants.AddRange(root, child);
        fixture.Db.Accounts.Add(account);
        await fixture.Db.SaveChangesAsync();
        var expiresAtUtc = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var result = await fixture.Service.SelectTenantAsync(account.Id, child.Id, expiresAtUtc, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expiresAtUtc, result.ExpiresAt);
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(result.Token).Claims.ToList();
        Assert.Equal(child.Id.ToString(), claims.Single(item => item.Type == "tenant_id").Value);
        Assert.Equal(AuthorizationProfile.Viewer.ToString(), claims.Single(item => item.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public async Task SelectTenant_returns_null_for_a_tenant_outside_the_authorized_set()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var root = new Tenant { Id = Guid.NewGuid(), Name = "Root" };
        var unrelated = new Tenant { Id = Guid.NewGuid(), Name = "Unrelated" };
        var account = new Account { Id = Guid.NewGuid(), TenantId = root.Id, Username = "viewer", PasswordHash = Passwords.Hash("password"), Authorization = AuthorizationProfile.Viewer };
        fixture.Db.Tenants.AddRange(root, unrelated);
        fixture.Db.Accounts.Add(account);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.SelectTenantAsync(account.Id, unrelated.Id, DateTime.UtcNow.AddHours(4), CancellationToken.None);

        Assert.Null(result);
    }

    private sealed class AuthFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public ApplicationDbContext Db { get; }
        public AuthService Service { get; }

        private AuthFixture(SqliteConnection connection, ApplicationDbContext db, AuthService service)
        {
            this.connection = connection;
            Db = db;
            Service = service;
        }

        public static async Task<AuthFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-that-is-long-enough-for-jwt",
                ["Jwt:Hours"] = "4"
            }).Build();
            return new AuthFixture(connection, db, new AuthService(db, configuration));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
