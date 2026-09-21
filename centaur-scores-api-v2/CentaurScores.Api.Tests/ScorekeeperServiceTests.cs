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
        var participant = Assert.Single(await db.MatchParticipants.AsNoTracking().Include(item => item.ParticipantListMember).Where(item => item.MatchId == matchId).ToListAsync());
        Assert.Equal(1, participant.Categories[disciplineId]);
        Assert.Equal(99, participant.Categories[classId]);
        var member = await db.ParticipantListMembers.AsNoTracking().SingleAsync(item => item.Id == memberId);
        Assert.False(member.Categories.ContainsKey(classId));
    }

    [Fact]
    public async Task GetMatchAsync_restricts_available_keys_to_matching_disabled_key_rules_and_allows_all_when_no_rule_matches()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var restrictedParticipantId = Guid.NewGuid();
        var unrestrictedParticipantId = Guid.NewGuid();
        db.AddRange(
            new Tenant { Id = tenantId, Name = "Tenant" },
            new Category
            {
                Id = classId,
                TenantId = tenantId,
                Name = "Class",
                Values =
                [
                    new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = classId, ValueId = 1, Name = "Cadet" },
                    new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = classId, ValueId = 2, Name = "Senior" }
                ]
            },
            new Match
            {
                Id = matchId,
                TenantId = tenantId,
                KeyboardJson = $$"""
                    {
                      "categoryOrder": ["{{classId}}"],
                      "keyboard": [
                        {"keyId": "X", "label": "X", "value": 10},
                        {"keyId": "10", "label": "10", "value": 10},
                        {"keyId": "M", "label": "M", "value": 0}
                      ],
                      "disabledKeyRules": [
                        {"categoryId": "{{classId}}", "valueId": 1, "disabledKeyIds": ["X"]}
                      ]
                    }
                    """,
                Devices = [new ScoreDevice { Id = deviceId, TenantId = tenantId, MatchId = matchId, Name = "Device" }],
                Participants =
                [
                    new MatchParticipant { Id = restrictedParticipantId, TenantId = tenantId, MatchId = matchId, DeviceId = deviceId, OwnFullName = "Restricted", OwnLastName = "Restricted", OwnCategories = new Dictionary<Guid, int> { [classId] = 1 } },
                    new MatchParticipant { Id = unrestrictedParticipantId, TenantId = tenantId, MatchId = matchId, DeviceId = deviceId, OwnFullName = "Unrestricted", OwnLastName = "Unrestricted", OwnCategories = new Dictionary<Guid, int> { [classId] = 2 } }
                ]
            });
        await db.SaveChangesAsync();

        var service = new ScorekeeperService(db, new PersonalBestLiveLookup(db, new PersonalBestContext(db), new PersonalBestEngine(db), new MemoryCache(new MemoryCacheOptions())));
        var context = await service.FindAsync(tenantId, matchId, deviceId, CancellationToken.None);
        Assert.NotNull(context);

        var result = await service.GetMatchAsync(context!, CancellationToken.None);

        var restricted = result.Participants.Single(item => item.MatchParticipantId == restrictedParticipantId);
        var unrestricted = result.Participants.Single(item => item.MatchParticipantId == unrestrictedParticipantId);
        Assert.Equal(["10", "M"], restricted.AvailableKeyIDs);
        Assert.Null(unrestricted.AvailableKeyIDs);
    }

    [Fact]
    public async Task SignParticipantAsync_signs_a_participant_assigned_to_the_calling_device()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        db.AddRange(
            new Tenant { Id = tenantId, Name = "Tenant" },
            new Match
            {
                Id = matchId,
                TenantId = tenantId,
                SignatureMode = "confirm",
                Devices = [new ScoreDevice { Id = deviceId, TenantId = tenantId, MatchId = matchId, Name = "Device" }],
                Participants = [new MatchParticipant { Id = participantId, TenantId = tenantId, MatchId = matchId, DeviceId = deviceId, OwnFullName = "Robin Archer", OwnLastName = "Archer" }]
            });
        await db.SaveChangesAsync();

        var service = new ScorekeeperService(db, new PersonalBestLiveLookup(db, new PersonalBestContext(db), new PersonalBestEngine(db), new MemoryCache(new MemoryCacheOptions())));
        var context = await service.FindAsync(tenantId, matchId, deviceId, CancellationToken.None);
        Assert.NotNull(context);

        var error = await service.SignParticipantAsync(context!, participantId, new SignParticipantRequest(null, null), CancellationToken.None);

        Assert.Null(error);
        var participant = await db.MatchParticipants.AsNoTracking().SingleAsync(item => item.Id == participantId);
        Assert.True(participant.Signed);
        Assert.NotNull(participant.SignedAtUtc);
    }

    [Fact]
    public async Task SignParticipantAsync_is_rejected_when_the_match_does_not_require_signing_or_the_card_is_already_signed()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var noneMatchId = Guid.NewGuid();
        var signedMatchId = Guid.NewGuid();
        var deviceOneId = Guid.NewGuid();
        var deviceTwoId = Guid.NewGuid();
        var unsignedParticipantId = Guid.NewGuid();
        var alreadySignedParticipantId = Guid.NewGuid();
        db.AddRange(
            new Tenant { Id = tenantId, Name = "Tenant" },
            new Match
            {
                Id = noneMatchId,
                TenantId = tenantId,
                SignatureMode = "none",
                Devices = [new ScoreDevice { Id = deviceOneId, TenantId = tenantId, MatchId = noneMatchId, Name = "Device" }],
                Participants = [new MatchParticipant { Id = unsignedParticipantId, TenantId = tenantId, MatchId = noneMatchId, DeviceId = deviceOneId }]
            },
            new Match
            {
                Id = signedMatchId,
                TenantId = tenantId,
                SignatureMode = "confirm",
                Devices = [new ScoreDevice { Id = deviceTwoId, TenantId = tenantId, MatchId = signedMatchId, Name = "Device" }],
                Participants = [new MatchParticipant { Id = alreadySignedParticipantId, TenantId = tenantId, MatchId = signedMatchId, DeviceId = deviceTwoId, Signed = true }]
            });
        await db.SaveChangesAsync();

        var service = new ScorekeeperService(db, new PersonalBestLiveLookup(db, new PersonalBestContext(db), new PersonalBestEngine(db), new MemoryCache(new MemoryCacheOptions())));
        var request = new SignParticipantRequest(null, null);

        var noneContext = await service.FindAsync(tenantId, noneMatchId, deviceOneId, CancellationToken.None);
        var noneError = await service.SignParticipantAsync(noneContext!, unsignedParticipantId, request, CancellationToken.None);
        Assert.Equal("SIGNATURE_NOT_REQUIRED", noneError?.Code);

        var signedContext = await service.FindAsync(tenantId, signedMatchId, deviceTwoId, CancellationToken.None);
        var signedError = await service.SignParticipantAsync(signedContext!, alreadySignedParticipantId, request, CancellationToken.None);
        Assert.Equal("SCORECARD_SIGNED", signedError?.Code);
    }

    [Fact]
    public async Task UpdateScoresAsync_rejects_updates_to_a_signed_participant()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        db.AddRange(
            new Tenant { Id = tenantId, Name = "Tenant" },
            new Match
            {
                Id = matchId,
                TenantId = tenantId,
                ArrowsPerEnd = 3,
                KeyboardJson = """{"categoryOrder":[],"keyboard":[{"keyId":"10","label":"10","value":10}]}""",
                Devices = [new ScoreDevice { Id = deviceId, TenantId = tenantId, MatchId = matchId, Name = "Device" }],
                Participants = [new MatchParticipant { Id = participantId, TenantId = tenantId, MatchId = matchId, DeviceId = deviceId, Signed = true }]
            });
        await db.SaveChangesAsync();

        var service = new ScorekeeperService(db, new PersonalBestLiveLookup(db, new PersonalBestContext(db), new PersonalBestEngine(db), new MemoryCache(new MemoryCacheOptions())));
        var context = await service.FindAsync(tenantId, matchId, deviceId, CancellationToken.None);
        Assert.NotNull(context);
        var request = new ScorekeeperScoreUpdates(participantId, [new ScorekeeperScoreUpdate(0, null, "10")]);

        var conflicts = await service.UpdateScoresAsync(context!, [request], CancellationToken.None);

        var conflict = Assert.Single(conflicts);
        Assert.Equal("SCORECARD_SIGNED", conflict.Error);
        Assert.Empty(await db.ArrowScores.AsNoTracking().Where(item => item.MatchParticipantId == participantId).ToListAsync());
    }
}
