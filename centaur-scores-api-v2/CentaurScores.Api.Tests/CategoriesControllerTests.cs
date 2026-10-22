using System.Text.Json;
using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Controllers;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Tests;

public sealed class CategoriesControllerTests
{
    [Fact]
    public async Task Update_stores_a_trimmed_api_name_and_an_empty_one_clears_it()
    {
        await using var fixture = await Fixture.CreateAsync();
        var categoryId = await fixture.AddCategoryAsync("Klasse");
        var controller = fixture.Controller();

        var saved = Assert.IsType<OkObjectResult>(await controller.Update(categoryId, new UpdateCategoryRequest("Klasse", "  klasse  "), CancellationToken.None));
        Assert.Equal("klasse", Assert.IsType<Category>(saved.Value).ApiName);
        Assert.Equal("klasse", (await fixture.Db.Categories.AsNoTracking().SingleAsync(item => item.Id == categoryId)).ApiName);

        Assert.IsType<OkObjectResult>(await controller.Update(categoryId, new UpdateCategoryRequest("Klasse", "   "), CancellationToken.None));
        Assert.Null((await fixture.Db.Categories.AsNoTracking().SingleAsync(item => item.Id == categoryId)).ApiName);
    }

    [Fact]
    public async Task Update_without_api_name_in_the_body_clears_it()
    {
        await using var fixture = await Fixture.CreateAsync();
        var categoryId = await fixture.AddCategoryAsync("Klasse", "klasse");

        Assert.IsType<OkObjectResult>(await fixture.Controller().Update(categoryId, new UpdateCategoryRequest("Class"), CancellationToken.None));

        var category = await fixture.Db.Categories.AsNoTracking().SingleAsync(item => item.Id == categoryId);
        Assert.Equal("Class", category.Name);
        Assert.Null(category.ApiName);
    }

    [Fact]
    public async Task Update_rejects_an_api_name_already_used_by_another_category_of_the_tenant()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddCategoryAsync("Klasse", "klasse");
        var otherId = await fixture.AddCategoryAsync("Discipline");

        var conflict = Assert.IsType<ConflictObjectResult>(await fixture.Controller().Update(otherId, new UpdateCategoryRequest("Discipline", "KLASSE"), CancellationToken.None));

        Assert.Equal("CATEGORY_API_NAME_TAKEN", Assert.IsType<ApiError>(conflict.Value).Code);
        Assert.Null((await fixture.Db.Categories.AsNoTracking().SingleAsync(item => item.Id == otherId)).ApiName);
    }

    [Fact]
    public async Task Update_allows_the_same_api_name_in_another_tenant_and_on_the_category_itself()
    {
        await using var fixture = await Fixture.CreateAsync();
        var categoryId = await fixture.AddCategoryAsync("Klasse", "klasse");
        await fixture.AddCategoryAsync("Klasse", "klasse", Guid.NewGuid());

        Assert.IsType<OkObjectResult>(await fixture.Controller().Update(categoryId, new UpdateCategoryRequest("Klasse 2", "klasse"), CancellationToken.None));
    }

    [Theory]
    [InlineData("score", "CATEGORY_API_NAME_RESERVED")]
    [InlineData("Name", "CATEGORY_API_NAME_RESERVED")]
    [InlineData("LIDNUMMER", "CATEGORY_API_NAME_RESERVED")]
    [InlineData("an-api-name-that-is-far-too-long-to-be-accepted-by-the-validation-rule", "CATEGORY_API_NAME_INVALID")]
    public async Task Update_rejects_reserved_and_too_long_api_names(string apiName, string expectedCode)
    {
        await using var fixture = await Fixture.CreateAsync();
        var categoryId = await fixture.AddCategoryAsync("Klasse");

        var badRequest = Assert.IsType<BadRequestObjectResult>(await fixture.Controller().Update(categoryId, new UpdateCategoryRequest("Klasse", apiName), CancellationToken.None));

        Assert.Equal(expectedCode, Assert.IsType<ApiError>(badRequest.Value).Code);
    }

    [Fact]
    public async Task Update_is_forbidden_for_a_viewer()
    {
        await using var fixture = await Fixture.CreateAsync();
        var categoryId = await fixture.AddCategoryAsync("Klasse");

        Assert.IsType<ForbidResult>(await fixture.Controller(canManage: false).Update(categoryId, new UpdateCategoryRequest("Klasse", "klasse"), CancellationToken.None));
    }

    [Fact]
    public async Task Backup_round_trip_keeps_the_api_name()
    {
        await using var fixture = await Fixture.CreateAsync();
        var adminTenantId = Guid.NewGuid();
        fixture.Db.Tenants.Add(new Tenant { Id = adminTenantId, Name = "HQ" });
        await fixture.AddCategoryAsync("Klasse", "klasse");

        var (zipBytes, _) = await new BackupService(fixture.Db).CreateBackupAsync(fixture.TenantId, includeSubTenants: false, CancellationToken.None);
        using var zipStream = new MemoryStream(zipBytes);
        var result = await new RestoreService(fixture.Db).RestoreAsync(adminTenantId, zipStream, CancellationToken.None);

        var restored = await fixture.Db.Categories.AsNoTracking().SingleAsync(item => item.TenantId == result.NewTenantId);
        Assert.Equal("klasse", restored.ApiName);
    }

    [Fact]
    public void Category_backup_written_before_api_names_existed_deserializes_without_one()
    {
        var json = $$"""{"Id":"{{Guid.NewGuid()}}","TenantId":"{{Guid.NewGuid()}}","Name":"Klasse","IsUsed":true,"Values":[{"ValueId":1,"Name":"Senioren"}]}""";

        var backup = JsonSerializer.Deserialize<CategoryBackup>(json);

        Assert.NotNull(backup);
        Assert.Null(backup.ApiName);
        Assert.Equal("Klasse", backup.Name);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public ApplicationDbContext Db { get; }
        public Guid TenantId { get; } = Guid.NewGuid();

        private Fixture(SqliteConnection connection, ApplicationDbContext db)
        {
            this.connection = connection;
            Db = db;
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var fixture = new Fixture(connection, db);
            db.Tenants.Add(new Tenant { Id = fixture.TenantId, Name = "Club" });
            await db.SaveChangesAsync();
            return fixture;
        }

        public CategoriesController Controller(bool canManage = true) => new(Db, new TestTenantContext(TenantId, canManage));

        public async Task<Guid> AddCategoryAsync(string name, string? apiName = null, Guid? tenantId = null)
        {
            var category = new Category { Id = Guid.NewGuid(), TenantId = tenantId ?? TenantId, Name = name, ApiName = apiName };
            Db.Categories.Add(category);
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
            return category.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class TestTenantContext(Guid tenantId, bool canManage) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid AccountId { get; } = Guid.NewGuid();
        public bool IsAdministrator => canManage;
        public bool CanManage => canManage;
        public DateTime TokenExpiresAtUtc => DateTime.UtcNow.AddHours(4);
    }
}
