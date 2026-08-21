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
    public async Task Parent_administrator_can_log_in_to_descendant_tenant()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var root = new Tenant { Id = Guid.NewGuid(), Name = "Root" };
        var child = new Tenant { Id = Guid.NewGuid(), Name = "Child", ParentTenantId = root.Id };
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = root.Id,
            Username = "admin",
            PasswordHash = Passwords.Hash("password"),
            Authorization = AuthorizationProfile.Administrator
        };
        fixture.Db.Tenants.AddRange(root, child);
        fixture.Db.Accounts.Add(account);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.AuthenticateAsync(new LoginRequest("admin", "password", child.Id), CancellationToken.None);

        Assert.NotNull(result);
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(result.Token).Claims.ToList();
        Assert.Equal(child.Id.ToString(), claims.Single(item => item.Type == "tenant_id").Value);
        Assert.Equal(AuthorizationProfile.Administrator.ToString(), claims.Single(item => item.Type == ClaimTypes.Role).Value);
        Assert.Equal(account.Id, result.Account.Id);
    }

    [Fact]
    public async Task Local_account_takes_precedence_over_parent_account()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var root = new Tenant { Id = Guid.NewGuid(), Name = "Root" };
        var child = new Tenant { Id = Guid.NewGuid(), Name = "Child", ParentTenantId = root.Id };
        fixture.Db.Tenants.AddRange(root, child);
        fixture.Db.Accounts.AddRange(
            new Account { Id = Guid.NewGuid(), TenantId = root.Id, Username = "admin", PasswordHash = Passwords.Hash("parent-password"), Authorization = AuthorizationProfile.Administrator },
            new Account { Id = Guid.NewGuid(), TenantId = child.Id, Username = "admin", PasswordHash = Passwords.Hash("local-password"), Authorization = AuthorizationProfile.Viewer });
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.AuthenticateAsync(new LoginRequest("admin", "parent-password", child.Id), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Non_administrator_parent_account_cannot_log_in_to_descendant_tenant()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var root = new Tenant { Id = Guid.NewGuid(), Name = "Root" };
        var child = new Tenant { Id = Guid.NewGuid(), Name = "Child", ParentTenantId = root.Id };
        fixture.Db.Tenants.AddRange(root, child);
        fixture.Db.Accounts.Add(new Account { Id = Guid.NewGuid(), TenantId = root.Id, Username = "user", PasswordHash = Passwords.Hash("password"), Authorization = AuthorizationProfile.Manager });
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.AuthenticateAsync(new LoginRequest("user", "password", child.Id), CancellationToken.None);

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