using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Controllers;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace CentaurScores.Api.Tests;

public sealed class PersonalBestControllerTests
{
    private static async Task<ApplicationDbContext> NewDbAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static PersonalBestController NewController(ApplicationDbContext db, Guid tenantId) =>
        new(db, new TestTenantContext(tenantId), new PersonalBestContext(db), new PersonalBestEngine(db), new PersonalBestExcelService());

    [Fact]
    public async Task SaveExportConfig_can_be_called_repeatedly_without_a_concurrency_exception()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Tenant" });
        await db.SaveChangesAsync();

        var controller = NewController(db, tenantId);
        Assert.IsType<OkObjectResult>(await controller.Enable(CancellationToken.None));

        var firstRequest = new SavePersonalBestExportConfigRequest("all", "Export", new List<SavePersonalBestExportColumnRequest>
        {
            new("Bondsnummer", "federationNumber", null),
            new("Datum", "date", "ymd")
        });
        var firstResult = Assert.IsType<OkObjectResult>(await controller.SaveExportConfig(firstRequest, CancellationToken.None));
        var firstView = Assert.IsType<PersonalBestExportConfigView>(firstResult.Value);
        Assert.Equal(2, firstView.Columns.Count);

        // Saving again (as the config edit page does every time) previously threw
        // DbUpdateConcurrencyException because config.Columns was reassigned on a still-tracked entity.
        var secondRequest = new SavePersonalBestExportConfigRequest("changesSinceLastImport", "Export2", new List<SavePersonalBestExportColumnRequest>
        {
            new("Naam", "fullName", null)
        });
        var secondResult = Assert.IsType<OkObjectResult>(await controller.SaveExportConfig(secondRequest, CancellationToken.None));
        var secondView = Assert.IsType<PersonalBestExportConfigView>(secondResult.Value);
        Assert.Equal("changesSinceLastImport", secondView.ExportMode);
        Assert.Equal("Export2", secondView.TableName);
        var column = Assert.Single(secondView.Columns);
        Assert.Equal("Naam", column.ColumnName);

        Assert.Equal(1, await db.PersonalBestExportColumns.CountAsync());
    }

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid AccountId { get; } = Guid.NewGuid();
        public bool IsAdministrator => true;
        public bool CanManage => true;
    }
}
