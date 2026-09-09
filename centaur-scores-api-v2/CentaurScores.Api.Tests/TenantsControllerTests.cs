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
        var controller = new TenantsController(db, new TestTenantContext(tenant.Id, canManage: true, isAdministrator: false), new NarrowcastScopeContext(db), new TenantDeletionService(db));

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
        var controller = new TenantsController(db, new TestTenantContext(tenant.Id, canManage: false, isAdministrator: false), new NarrowcastScopeContext(db), new TenantDeletionService(db));

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
        var controller = new TenantsController(db, new TestTenantContext(parent.Id, canManage: true, isAdministrator: true), new NarrowcastScopeContext(db), new TenantDeletionService(db));

        await controller.Create(new CreateTenantRequest("Child", null, parent.Id, "centaurhal"), CancellationToken.None);

        var child = await db.Tenants.SingleAsync(item => item.Name == "Child");
        Assert.Equal("centaurhal", child.DefaultNarrowcastScope);
    }

    [Fact]
    public async Task Delete_removes_all_data_owned_by_the_tenant_and_its_descendant_tenants()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);

        var grandparentId = Guid.NewGuid(); // caller's own tenant - stays untouched
        var parentId = Guid.NewGuid();      // the tenant being deleted
        var childId = Guid.NewGuid();       // a descendant of the deleted tenant - must be deleted too
        var categoryId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();

        db.Tenants.AddRange(
            new Tenant { Id = grandparentId, Name = "HQ" },
            new Tenant { Id = parentId, Name = "Club", ParentTenantId = grandparentId },
            new Tenant { Id = childId, Name = "Sub-club", ParentTenantId = parentId });

        // Data directly on the deleted tenant.
        db.Accounts.Add(new Account { Id = Guid.NewGuid(), TenantId = parentId, Username = "coach", PasswordHash = "x", Authorization = AuthorizationProfile.Manager });
        db.Categories.Add(new Category { Id = categoryId, TenantId = parentId, Name = "Bow type", Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = parentId, CategoryId = categoryId, ValueId = 1, Name = "Recurve" }] });
        db.ParticipantLists.Add(new ParticipantList { Id = listId, TenantId = parentId, Name = "Members", Members = [new ParticipantListMember { Id = memberId, TenantId = parentId, ParticipantListId = listId, LastName = "Archer", FullName = "Robin Archer" }] });
        db.Matches.Add(new Match
        {
            Id = matchId,
            TenantId = parentId,
            Name = "Open",
            ParticipantListId = listId,
            Devices = [new ScoreDevice { Id = deviceId, TenantId = parentId, MatchId = matchId, Name = "Target 1" }],
            Participants = [new MatchParticipant { Id = participantId, TenantId = parentId, MatchId = matchId, ParticipantListMemberId = memberId, DeviceId = deviceId, Scores = [new ArrowScore { Id = Guid.NewGuid(), TenantId = parentId, MatchParticipantId = participantId, End = 1, Arrow = 1, KeyId = "X", Value = 10 }] }]
        });
        db.Competitions.Add(new Competition
        {
            Id = competitionId,
            TenantId = parentId,
            Name = "Series",
            Rounds = [new CompetitionRound { Id = roundId, TenantId = parentId, CompetitionId = competitionId, ShortName = "R1", LongName = "Round 1", Matches = [new CompetitionRoundMatch { Id = Guid.NewGuid(), TenantId = parentId, CompetitionRoundId = roundId, MatchId = matchId }] }],
            ScoringRules = [new CompetitionScoreRule { Id = Guid.NewGuid(), TenantId = parentId, CompetitionId = competitionId, Name = "Total" }]
        });

        // Data on the descendant (child) tenant - must be deleted as part of deleting the parent.
        db.Categories.Add(new Category { Id = Guid.NewGuid(), TenantId = childId, Name = "Child category" });

        // A discipline/mapping OWNED by the grandparent (not being deleted), but whose mapping references a
        // category on the deleted parent tenant via SourceTenantId - this must be dropped rather than left
        // dangling, while the grandparent's own discipline row survives.
        db.PersonalBestDisciplines.Add(new PersonalBestDiscipline
        {
            Id = disciplineId,
            TenantId = grandparentId,
            Name = "18m Recurve",
            Mappings = [new PersonalBestDisciplineMapping { Id = Guid.NewGuid(), TenantId = grandparentId, SourceTenantId = parentId, CategoryId = categoryId, ValueId = 1 }]
        });

        await db.SaveChangesAsync();

        var controller = new TenantsController(db, new TestTenantContext(grandparentId, canManage: true, isAdministrator: true), new NarrowcastScopeContext(db), new TenantDeletionService(db));

        var result = await controller.Delete(parentId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.False(await db.Tenants.AnyAsync(item => item.Id == parentId));
        Assert.False(await db.Tenants.AnyAsync(item => item.Id == childId));
        Assert.False(await db.Accounts.AnyAsync(item => item.TenantId == parentId));
        Assert.False(await db.Categories.AnyAsync(item => item.TenantId == parentId || item.TenantId == childId));
        Assert.False(await db.CategoryValues.AnyAsync(item => item.TenantId == parentId));
        Assert.False(await db.ParticipantLists.AnyAsync(item => item.TenantId == parentId));
        Assert.False(await db.ParticipantListMembers.AnyAsync(item => item.TenantId == parentId));
        Assert.False(await db.Matches.AnyAsync(item => item.TenantId == parentId));
        Assert.False(await db.MatchParticipants.AnyAsync(item => item.TenantId == parentId));
        Assert.False(await db.ArrowScores.AnyAsync(item => item.TenantId == parentId));
        Assert.False(await db.ScoreDevices.AnyAsync(item => item.TenantId == parentId));
        Assert.False(await db.Competitions.AnyAsync(item => item.TenantId == parentId));
        Assert.False(await db.CompetitionRounds.AnyAsync(item => item.TenantId == parentId));
        Assert.False(await db.CompetitionRoundMatches.AnyAsync(item => item.TenantId == parentId));
        Assert.False(await db.CompetitionScoreRules.AnyAsync(item => item.TenantId == parentId));
        Assert.False(await db.PersonalBestDisciplineMappings.AnyAsync(item => item.SourceTenantId == parentId));

        // The caller's own tenant and its unrelated data are untouched.
        Assert.True(await db.Tenants.AnyAsync(item => item.Id == grandparentId));
        var survivingDiscipline = await db.PersonalBestDisciplines.SingleAsync(item => item.Id == disciplineId);
        Assert.Equal(grandparentId, survivingDiscipline.TenantId);
    }

    [Fact]
    public async Task Delete_is_forbidden_for_a_tenant_that_is_not_a_direct_child_of_the_caller()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var callerId = Guid.NewGuid();
        var unrelatedId = Guid.NewGuid();
        db.Tenants.AddRange(new Tenant { Id = callerId, Name = "Caller" }, new Tenant { Id = unrelatedId, Name = "Unrelated" });
        await db.SaveChangesAsync();
        var controller = new TenantsController(db, new TestTenantContext(callerId, canManage: true, isAdministrator: true), new NarrowcastScopeContext(db), new TenantDeletionService(db));

        Assert.IsType<NotFoundResult>(await controller.Delete(unrelatedId, CancellationToken.None));
        Assert.True(await db.Tenants.AnyAsync(item => item.Id == unrelatedId));
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
