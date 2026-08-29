using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Controllers;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Tests;

public sealed class TenantsControllerTests
{
    private static async Task<ApplicationDbContext> NewDbAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task UpdateCurrentDefaultScope_lets_a_manager_set_and_clear_their_own_tenants_value()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Mine" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        var controller = new TenantsController(db, new TestTenantContext(tenant.Id, canManage: true, isAdministrator: false), new NarrowcastScopeContext(db));

        var setResult = Assert.IsType<OkObjectResult>(await controller.UpdateCurrentDefaultScope(new UpdateDefaultNarrowcastScopeRequest("centaurhal"), CancellationToken.None));
        var setSettings = Assert.IsType<DefaultScopeSettings>(setResult.Value);
        Assert.Equal("centaurhal", setSettings.TenantValue);
        Assert.Equal("centaurhal", setSettings.EffectiveValue);

        var clearResult = Assert.IsType<OkObjectResult>(await controller.UpdateCurrentDefaultScope(new UpdateDefaultNarrowcastScopeRequest(null), CancellationToken.None));
        var clearSettings = Assert.IsType<DefaultScopeSettings>(clearResult.Value);
        Assert.Null(clearSettings.TenantValue);
        Assert.Equal("all", clearSettings.EffectiveValue);
    }

    [Fact]
    public async Task UpdateCurrentDefaultScope_is_forbidden_without_manage_rights()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Mine" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        var controller = new TenantsController(db, new TestTenantContext(tenant.Id, canManage: false, isAdministrator: false), new NarrowcastScopeContext(db));

        Assert.IsType<ForbidResult>(await controller.UpdateCurrentDefaultScope(new UpdateDefaultNarrowcastScopeRequest("centaurhal"), CancellationToken.None));
    }

    [Fact]
    public async Task Create_stores_the_default_narrowcast_scope_override_for_a_new_sub_tenant()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var parent = new Tenant { Id = Guid.NewGuid(), Name = "Parent" };
        db.Tenants.Add(parent);
        await db.SaveChangesAsync();
        var controller = new TenantsController(db, new TestTenantContext(parent.Id, canManage: true, isAdministrator: true), new NarrowcastScopeContext(db));

        await controller.Create(new CreateTenantRequest("Child", null, parent.Id, "centaurhal"), CancellationToken.None);

        var child = await db.Tenants.SingleAsync(item => item.Name == "Child");
        Assert.Equal("centaurhal", child.DefaultNarrowcastScope);
    }

    private sealed class TestTenantContext(Guid tenantId, bool canManage = true, bool isAdministrator = true) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid AccountId { get; } = Guid.NewGuid();
        public bool IsAdministrator => isAdministrator;
        public bool CanManage => canManage || isAdministrator;
        public DateTime TokenExpiresAtUtc => DateTime.UtcNow.AddHours(4);
    }
}
