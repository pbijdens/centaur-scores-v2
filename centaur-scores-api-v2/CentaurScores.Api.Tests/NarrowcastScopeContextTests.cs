using CentaurScores.Api.Application;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Tests;

public sealed class NarrowcastScopeContextTests
{
    private static async Task<ApplicationDbContext> NewDbAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task ResolveEffectiveScopeAsync_falls_back_to_all_when_nothing_configured_anywhere()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Leaf" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var scope = await new NarrowcastScopeContext(db).ResolveEffectiveScopeAsync(tenant.Id, CancellationToken.None);

        Assert.Equal("all", scope);
    }

    [Fact]
    public async Task ResolveEffectiveScopeAsync_uses_the_tenants_own_value_when_set()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Root", DefaultNarrowcastScope = "centaurhal" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var scope = await new NarrowcastScopeContext(db).ResolveEffectiveScopeAsync(tenant.Id, CancellationToken.None);

        Assert.Equal("centaurhal", scope);
    }

    [Fact]
    public async Task ResolveEffectiveScopeAsync_inherits_from_the_nearest_ancestor_that_set_one()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var grandparent = new Tenant { Id = Guid.NewGuid(), Name = "Grandparent", DefaultNarrowcastScope = "centaurhal" };
        var parent = new Tenant { Id = Guid.NewGuid(), Name = "Parent", ParentTenantId = grandparent.Id };
        var child = new Tenant { Id = Guid.NewGuid(), Name = "Child", ParentTenantId = parent.Id };
        db.AddRange(grandparent, parent, child);
        await db.SaveChangesAsync();

        var scope = await new NarrowcastScopeContext(db).ResolveEffectiveScopeAsync(child.Id, CancellationToken.None);

        Assert.Equal("centaurhal", scope);
    }

    [Fact]
    public async Task ResolveEffectiveScopeAsync_stops_at_the_nearest_ancestor_override()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var grandparent = new Tenant { Id = Guid.NewGuid(), Name = "Grandparent", DefaultNarrowcastScope = "grandparent-scope" };
        var parent = new Tenant { Id = Guid.NewGuid(), Name = "Parent", ParentTenantId = grandparent.Id, DefaultNarrowcastScope = "parent-scope" };
        var child = new Tenant { Id = Guid.NewGuid(), Name = "Child", ParentTenantId = parent.Id };
        db.AddRange(grandparent, parent, child);
        await db.SaveChangesAsync();

        var scope = await new NarrowcastScopeContext(db).ResolveEffectiveScopeAsync(child.Id, CancellationToken.None);

        Assert.Equal("parent-scope", scope);
    }
}
