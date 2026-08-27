using ClosedXML.Excel;
using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Controllers;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Tests;

public sealed class ParticipantListsControllerTests
{
    [Fact]
    public async Task Export_writes_translated_headers_sorted_rows_and_metadata_tables()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var list = new ParticipantList
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Club members",
            Members =
            [
                new ParticipantListMember { Id = Guid.NewGuid(), TenantId = tenantId, LastName = "Archer", FullName = "Amy Archer", FederationNumber = "1", IsActive = true, Categories = new Dictionary<Guid, int> { [classId] = 1 } },
                new ParticipantListMember { Id = Guid.NewGuid(), TenantId = tenantId, LastName = "Bowman", FullName = "Bob Bowman", FederationNumber = "2", IsActive = false, Categories = [] },
                new ParticipantListMember { Id = Guid.NewGuid(), TenantId = tenantId, LastName = "Archer", FullName = "Zack Archer", FederationNumber = "3", IsActive = true, Categories = new Dictionary<Guid, int> { [classId] = 99 } }
            ]
        };
        db.AddRange(
            new Tenant { Id = tenantId, Name = "Tenant" },
            new Category { Id = classId, TenantId = tenantId, Name = "Klasse", Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = classId, ValueId = 1, Name = "Senior" }] },
            list);
        await db.SaveChangesAsync();

        var controller = new ParticipantListsController(db, new TestTenantContext(tenantId), new ParticipantListExcelService());

        var result = Assert.IsType<FileContentResult>(await controller.Export(list.Id, "nl", CancellationToken.None));
        Assert.Equal("Club members.xlsx", result.FileDownloadName);

        using var workbook = new XLWorkbook(new MemoryStream(result.FileContents));
        var data = workbook.Worksheet("Data");
        Assert.Equal("Bondsnummer", data.Cell(1, 1).GetString());
        Assert.Equal("Naam", data.Cell(1, 2).GetString());
        Assert.Equal("Achternaam", data.Cell(1, 3).GetString());
        Assert.Equal("Actief", data.Cell(1, 4).GetString());
        Assert.Equal("Klasse", data.Cell(1, 5).GetString());

        // Active members first (Amy/Zack), then inactive (Bob) last; alphabetical by last name within each group.
        Assert.Equal("Amy Archer", data.Cell(2, 2).GetString());
        Assert.Equal("Senior", data.Cell(2, 5).GetString());
        Assert.Equal("Zack Archer", data.Cell(3, 2).GetString());
        Assert.Equal("Onbekend", data.Cell(3, 5).GetString());
        Assert.Equal("Bob Bowman", data.Cell(4, 2).GetString());
        Assert.False(data.Cell(4, 4).GetBoolean());

        var metadata = workbook.Worksheet("Metadata");
        Assert.Equal("Klasse", metadata.Cell(1, 1).GetString());
        Assert.Equal("Onbekend", metadata.Cell(3, 1).GetString());
        Assert.Contains(data.Tables, table => table.Name == "Data");
        Assert.Single(metadata.Tables);
    }

    [Fact]
    public async Task Export_is_forbidden_for_non_managers()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var controller = new ParticipantListsController(db, new TestTenantContext(Guid.NewGuid(), canManage: false), new ParticipantListExcelService());
        Assert.IsType<ForbidResult>(await controller.Export(Guid.NewGuid(), "en", CancellationToken.None));
    }

    [Fact]
    public async Task Import_updates_matching_federation_numbers_and_creates_the_rest()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var list = new ParticipantList
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Club members",
            Members = [new ParticipantListMember { Id = Guid.NewGuid(), TenantId = tenantId, LastName = "Old", FullName = "Old Name", FederationNumber = "1", IsActive = true }]
        };
        db.AddRange(
            new Tenant { Id = tenantId, Name = "Tenant" },
            new Category { Id = classId, TenantId = tenantId, Name = "Klasse", Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = classId, ValueId = 1, Name = "Senior" }] },
            list);
        await db.SaveChangesAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Data");
        sheet.Cell(1, 1).Value = "Number";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(1, 3).Value = "Last name";
        sheet.Cell(1, 4).Value = "Active";
        sheet.Cell(1, 5).Value = "Klasse";
        sheet.Cell(2, 1).Value = "1";
        sheet.Cell(2, 2).Value = "Updated Name";
        sheet.Cell(2, 3).Value = "Updated";
        sheet.Cell(2, 4).Value = true;
        sheet.Cell(2, 5).Value = "Senior";
        sheet.Cell(3, 1).Value = "2";
        sheet.Cell(3, 2).Value = "Brand New";
        sheet.Cell(3, 3).Value = "New";
        sheet.Cell(3, 4).Value = false;
        sheet.Cell(3, 5).Value = "Unknown";
        using var fileStream = new MemoryStream();
        workbook.SaveAs(fileStream);
        fileStream.Position = 0;
        var file = new FormFile(fileStream, 0, fileStream.Length, "file", "import.xlsx");

        var controller = new ParticipantListsController(db, new TestTenantContext(tenantId), new ParticipantListExcelService());
        var result = Assert.IsType<OkObjectResult>(await controller.Import(list.Id, file, CancellationToken.None));
        var summary = Assert.IsType<ImportParticipantListResult>(result.Value);
        Assert.Equal(1, summary.Created);
        Assert.Equal(1, summary.Updated);

        var updated = await db.ParticipantListMembers.SingleAsync(member => member.FederationNumber == "1");
        Assert.Equal("Updated Name", updated.FullName);
        Assert.Equal(1, updated.Categories[classId]);

        var created = await db.ParticipantListMembers.SingleAsync(member => member.FederationNumber == "2");
        Assert.Equal("Brand New", created.FullName);
        Assert.False(created.IsActive);
        Assert.Empty(created.Categories);
    }

    [Fact]
    public async Task Import_rejects_files_with_unrecognized_headers()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var list = new ParticipantList { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Club members" };
        db.AddRange(new Tenant { Id = tenantId, Name = "Tenant" }, list);
        await db.SaveChangesAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Data");
        sheet.Cell(1, 1).Value = "Not A Real Header";
        using var fileStream = new MemoryStream();
        workbook.SaveAs(fileStream);
        fileStream.Position = 0;
        var file = new FormFile(fileStream, 0, fileStream.Length, "file", "import.xlsx");

        var controller = new ParticipantListsController(db, new TestTenantContext(tenantId), new ParticipantListExcelService());
        var result = Assert.IsType<BadRequestObjectResult>(await controller.Import(list.Id, file, CancellationToken.None));
        var error = Assert.IsType<ApiError>(result.Value);
        Assert.Equal("IMPORT_UNRECOGNIZED_HEADERS", error.Code);
    }

    [Fact]
    public async Task List_returns_member_counts_without_the_members_themselves()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var list = new ParticipantList
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Club members",
            Members =
            [
                new ParticipantListMember { Id = Guid.NewGuid(), TenantId = tenantId, LastName = "Archer", FullName = "Amy Archer", IsActive = true },
                new ParticipantListMember { Id = Guid.NewGuid(), TenantId = tenantId, LastName = "Bowman", FullName = "Bob Bowman", IsActive = false }
            ]
        };
        db.AddRange(new Tenant { Id = tenantId, Name = "Tenant" }, list);
        await db.SaveChangesAsync();

        var controller = new ParticipantListsController(db, new TestTenantContext(tenantId), new ParticipantListExcelService());
        var result = Assert.IsType<OkObjectResult>(await controller.List(true, CancellationToken.None));
        var items = Assert.IsAssignableFrom<IReadOnlyList<ParticipantListSummary>>(result.Value);
        var summary = Assert.Single(items);

        Assert.Equal(2, summary.MemberCount);
        Assert.Equal(1, summary.ActiveMemberCount);
    }

    [Fact]
    public async Task Get_returns_the_list_with_its_members()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var list = new ParticipantList
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Club members",
            Members = [new ParticipantListMember { Id = Guid.NewGuid(), TenantId = tenantId, LastName = "Archer", FullName = "Amy Archer" }]
        };
        db.AddRange(new Tenant { Id = tenantId, Name = "Tenant" }, list);
        await db.SaveChangesAsync();

        var controller = new ParticipantListsController(db, new TestTenantContext(tenantId), new ParticipantListExcelService());
        var result = Assert.IsType<OkObjectResult>(await controller.Get(list.Id, CancellationToken.None));
        var returned = Assert.IsType<ParticipantList>(result.Value);

        Assert.Single(returned.Members);
    }

    [Fact]
    public async Task Get_returns_not_found_for_a_list_in_another_tenant()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var otherTenantId = Guid.NewGuid();
        var list = new ParticipantList { Id = Guid.NewGuid(), TenantId = otherTenantId, Name = "Club members" };
        db.AddRange(new Tenant { Id = otherTenantId, Name = "Tenant" }, list);
        await db.SaveChangesAsync();

        var controller = new ParticipantListsController(db, new TestTenantContext(Guid.NewGuid()), new ParticipantListExcelService());
        Assert.IsType<NotFoundResult>(await controller.Get(list.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_removes_the_list_and_its_members()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var list = new ParticipantList
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Club members",
            Members = [new ParticipantListMember { Id = Guid.NewGuid(), TenantId = tenantId, LastName = "Archer", FullName = "Amy Archer" }]
        };
        db.AddRange(new Tenant { Id = tenantId, Name = "Tenant" }, list);
        await db.SaveChangesAsync();

        var controller = new ParticipantListsController(db, new TestTenantContext(tenantId), new ParticipantListExcelService());
        Assert.IsType<NoContentResult>(await controller.Delete(list.Id, CancellationToken.None));

        Assert.False(await db.ParticipantLists.AnyAsync(item => item.Id == list.Id));
        Assert.False(await db.ParticipantListMembers.AnyAsync(item => item.ParticipantListId == list.Id));
    }

    [Fact]
    public async Task Delete_is_forbidden_for_non_managers()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var list = new ParticipantList { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Club members" };
        db.AddRange(new Tenant { Id = tenantId, Name = "Tenant" }, list);
        await db.SaveChangesAsync();

        var controller = new ParticipantListsController(db, new TestTenantContext(tenantId, canManage: false), new ParticipantListExcelService());
        Assert.IsType<ForbidResult>(await controller.Delete(list.Id, CancellationToken.None));
    }

    private sealed class TestTenantContext(Guid tenantId, bool canManage = true) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid AccountId { get; } = Guid.NewGuid();
        public bool IsAdministrator => true;
        public bool CanManage => canManage;
    }
}
