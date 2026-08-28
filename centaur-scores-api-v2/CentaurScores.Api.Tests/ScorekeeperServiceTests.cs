using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CentaurScores.Api.Tests;

public sealed class ScorekeeperServiceTests
{
    [Fact]
    public async Task GetMatchAsync_only_returns_categories_configured_for_the_match()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var unusedId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        db.AddRange(
            new Tenant { Id = tenantId, Name = "Tenant" },
            new Category { Id = disciplineId, TenantId = tenantId, Name = "Discipline", Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = disciplineId, ValueId = 1, Name = "Recurve" }] },
            new Category { Id = classId, TenantId = tenantId, Name = "Class", Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = classId, ValueId = 2, Name = "Senior" }] },
            new Category { Id = unusedId, TenantId = tenantId, Name = "Unused", Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = unusedId, ValueId = 3, Name = "SomeValue" }] },
            new Match
            {
                Id = matchId,
                TenantId = tenantId,
                KeyboardJson = $$"""{"categoryOrder":["{{classId}}","{{disciplineId}}"]}""",
                Devices = [new ScoreDevice { Id = deviceId, TenantId = tenantId, MatchId = matchId, Name = "Device" }]
            });
        await db.SaveChangesAsync();

        var service = new ScorekeeperService(db, new PersonalBestLiveLookup(db, new PersonalBestContext(db), new PersonalBestEngine(db), new MemoryCache(new MemoryCacheOptions())));
        var context = await service.FindAsync(tenantId, matchId, deviceId, CancellationToken.None);
        Assert.NotNull(context);

        var result = await service.GetMatchAsync(context!, CancellationToken.None);

        Assert.Collection(result.Categories,
            category => Assert.Equal(classId, category.Id),
            category => Assert.Equal(disciplineId, category.Id));
    }

    [Fact]
    public async Task SetParticipantsAsync_fills_missing_category_with_unknown_value_when_available_and_leaves_it_unset_otherwise()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        db.AddRange(
            new Tenant { Id = tenantId, Name = "Tenant" },
            new Category { Id = disciplineId, TenantId = tenantId, Name = "Discipline", Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = disciplineId, ValueId = 1, Name = "Recurve" }] },
            new Category
            {
                Id = classId,
                TenantId = tenantId,
                Name = "Class",
                Values =
                [
                    new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = classId, ValueId = 2, Name = "Senior" },
                    new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = classId, ValueId = 99, Name = "Onbekend" }
                ]
            },
            new Match
            {
                Id = matchId,
                TenantId = tenantId,
                AllowFreeParticipants = true,
                KeyboardJson = $$"""{"categoryOrder":["{{disciplineId}}","{{classId}}"]}""",
                Devices = [new ScoreDevice { Id = deviceId, TenantId = tenantId, MatchId = matchId, Name = "Device" }]
            });
        await db.SaveChangesAsync();

        var service = new ScorekeeperService(db, new PersonalBestLiveLookup(db, new PersonalBestContext(db), new PersonalBestEngine(db), new MemoryCache(new MemoryCacheOptions())));
        var context = await service.FindAsync(tenantId, matchId, deviceId, CancellationToken.None);
        Assert.NotNull(context);
        var request = new ScorekeeperParticipantRequest(null, null, "123", "Robin Archer", null,
            [
                new ScorekeeperParticipantCategory(disciplineId, "Discipline", null),
                new ScorekeeperParticipantCategory(classId, "Class", null)
            ], null, null);

        var error = await service.SetParticipantsAsync(context!, [request], CancellationToken.None);

        Assert.Null(error);
        var participant = Assert.Single(await db.MatchParticipants.AsNoTracking().Where(item => item.MatchId == matchId).ToListAsync());
        Assert.False(participant.Categories.ContainsKey(disciplineId));
        Assert.Equal(99, participant.Categories[classId]);
    }

    [Fact]
    public async Task SetParticipantsAsync_assigning_a_list_member_fills_missing_categories_without_mutating_the_list_member()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        db.AddRange(
            new Tenant { Id = tenantId, Name = "Tenant" },
            new Category { Id = disciplineId, TenantId = tenantId, Name = "Discipline", Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = disciplineId, ValueId = 1, Name = "Recurve" }] },
            new Category
            {
                Id = classId,
                TenantId = tenantId,
                Name = "Class",
                Values =
                [
                    new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = classId, ValueId = 2, Name = "Senior" },
                    new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = classId, ValueId = 99, Name = "Unknown" }
                ]
            },
            new ParticipantList { Id = listId, TenantId = tenantId, Name = "List" },
            new ParticipantListMember { Id = memberId, TenantId = tenantId, ParticipantListId = listId, LastName = "Archer", FullName = "Robin Archer", Categories = new Dictionary<Guid, int> { [disciplineId] = 1 } },
            new Match
            {
                Id = matchId,
                TenantId = tenantId,
                ParticipantListId = listId,
                KeyboardJson = $$"""{"categoryOrder":["{{disciplineId}}","{{classId}}"]}""",
                Devices = [new ScoreDevice { Id = deviceId, TenantId = tenantId, MatchId = matchId, Name = "Device" }]
            });
        await db.SaveChangesAsync();

        var service = new ScorekeeperService(db, new PersonalBestLiveLookup(db, new PersonalBestContext(db), new PersonalBestEngine(db), new MemoryCache(new MemoryCacheOptions())));
        var context = await service.FindAsync(tenantId, matchId, deviceId, CancellationToken.None);
        Assert.NotNull(context);
        var request = new ScorekeeperParticipantRequest(null, memberId, null, null, null, null, null, null);

        var error = await service.SetParticipantsAsync(context!, [request], CancellationToken.None);

        Assert.Null(error);
        var participant = Assert.Single(await db.MatchParticipants.AsNoTracking().Where(item => item.MatchId == matchId).ToListAsync());
        Assert.Equal(1, participant.Categories[disciplineId]);
        Assert.Equal(99, participant.Categories[classId]);
        var member = await db.ParticipantListMembers.AsNoTracking().SingleAsync(item => item.Id == memberId);
        Assert.False(member.Categories.ContainsKey(classId));
    }
}
