using CentaurScores.Api.Application;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Tests;

public sealed class PersonalBestContextTests
{
    private static async Task<ApplicationDbContext> NewDbAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task ResolveOwningTenantIdAsync_returns_null_when_disabled_everywhere()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Leaf" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var status = await new PersonalBestContext(db).GetStatusAsync(tenant.Id, CancellationToken.None);

        Assert.False(status.Enabled);
        Assert.False(status.OwnedHere);
        Assert.Null(status.OwningTenantId);
    }

    [Fact]
    public async Task ResolveOwningTenantIdAsync_finds_it_enabled_on_this_tenant()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Root", PersonalBestEnabled = true };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var status = await new PersonalBestContext(db).GetStatusAsync(tenant.Id, CancellationToken.None);

        Assert.True(status.Enabled);
        Assert.True(status.OwnedHere);
        Assert.Equal(tenant.Id, status.OwningTenantId);
    }

    [Fact]
    public async Task ResolveOwningTenantIdAsync_walks_up_multiple_ancestors_to_find_the_owning_tenant()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var grandparent = new Tenant { Id = Guid.NewGuid(), Name = "Grandparent", PersonalBestEnabled = true };
        var parent = new Tenant { Id = Guid.NewGuid(), Name = "Parent", ParentTenantId = grandparent.Id };
        var child = new Tenant { Id = Guid.NewGuid(), Name = "Child", ParentTenantId = parent.Id };
        db.AddRange(grandparent, parent, child);
        await db.SaveChangesAsync();

        var status = await new PersonalBestContext(db).GetStatusAsync(child.Id, CancellationToken.None);

        Assert.True(status.Enabled);
        Assert.False(status.OwnedHere); // inherited, not enabled on this tenant itself - the Overview button stays hidden
        Assert.Equal(grandparent.Id, status.OwningTenantId);
    }

    [Fact]
    public async Task ResolveOwningTenantIdAsync_stops_at_the_nearest_enabled_ancestor()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var grandparent = new Tenant { Id = Guid.NewGuid(), Name = "Grandparent", PersonalBestEnabled = true };
        var parent = new Tenant { Id = Guid.NewGuid(), Name = "Parent", ParentTenantId = grandparent.Id, PersonalBestEnabled = true };
        var child = new Tenant { Id = Guid.NewGuid(), Name = "Child", ParentTenantId = parent.Id };
        db.AddRange(grandparent, parent, child);
        await db.SaveChangesAsync();

        var owningTenantId = await new PersonalBestContext(db).ResolveOwningTenantIdAsync(child.Id, CancellationToken.None);

        Assert.Equal(parent.Id, owningTenantId);
    }
}
