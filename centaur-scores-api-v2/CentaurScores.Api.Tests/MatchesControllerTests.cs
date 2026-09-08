using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Controllers;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

namespace CentaurScores.Api.Tests;

public sealed class MatchesControllerTests
{
    [Fact]
    public async Task Export_includes_key_counts_nulls_splits_categories_and_last_name_in_match_order()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var match = new Match
        {
            Id = matchId,
            TenantId = tenantId,
            Name = "Open",
            ShortCode = "OPEN",
            Ends = 4,
            ArrowsPerEnd = 2,
            GroupEnds = 2,
            KeyboardJson = $$"""{"categoryOrder":["{{classId}}","{{disciplineId}}"],"keyboard":[{"keyId":"M","label":"Miss"},{"keyId":"X","label":"X"}]}""",
            Participants =
            [
                new MatchParticipant
                {
                    Id = participantId,
                    TenantId = tenantId,
                    MatchId = matchId,
                    OwnFederationNumber = "123",
                    OwnFullName = "Robin Archer",
                    OwnLastName = "Archer",
                    OwnCategories = new Dictionary<Guid, int> { [disciplineId] = 7, [classId] = 2 },
                    Scores =
                    [
                        Score(tenantId, participantId, 1, 1, "X", 10),
                        Score(tenantId, participantId, 1, 2, "M", 0),
                        Score(tenantId, participantId, 3, 1, "X", 10)
                    ]
                }
            ]
        };
        db.AddRange(
            new Tenant { Id = tenantId, Name = "Tenant" },
            new Category { Id = disciplineId, TenantId = tenantId, Name = "Discipline", Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = disciplineId, ValueId = 7, Name = "Recurve" }] },
            new Category { Id = classId, TenantId = tenantId, Name = "Class", Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = classId, ValueId = 2, Name = "Senior" }] },
            match);
        await db.SaveChangesAsync();
        var scoring = new ScoringService();
        var personalBestContext = new PersonalBestContext(db);
        var personalBestEngine = new PersonalBestEngine(db);
        var controller = new MatchesController(db, new TestTenantContext(tenantId), scoring, new LiveScoringService(scoring), new PersonalBestRegistrationService(db, personalBestContext, personalBestEngine), new PersonalBestLiveLookup(db, personalBestContext, personalBestEngine, new MemoryCache(new MemoryCacheOptions())));

        var result = Assert.IsType<FileContentResult>(await controller.Export(matchId, CancellationToken.None));

        Assert.Equal("OPEN.csv", result.FileDownloadName);
        Assert.Equal(
            "federation_number,full_name,total,\"Miss\",\"X\",Null,Split1,Split2,\"Class\",\"Discipline\",lastname\n" +
            "\"123\",\"Robin Archer\",20,1,2,5,10,10,\"Senior\",\"Recurve\",\"Archer\"",
            Encoding.UTF8.GetString(result.FileContents));
    }

    [Fact]
    public async Task List_returns_participant_counts_without_the_participants_themselves()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var listedParticipantId = Guid.NewGuid();
        var listMemberId = Guid.NewGuid();
        var match = new Match
        {
            Id = matchId,
            TenantId = tenantId,
            Name = "Open",
            Participants =
            [
                new MatchParticipant { Id = listedParticipantId, TenantId = tenantId, MatchId = matchId, ParticipantListMemberId = listMemberId, ParticipantListMember = new ParticipantListMember { Id = listMemberId, TenantId = tenantId, ParticipantListId = listId, LastName = "Listed", FullName = "Listed Archer" } },
                new MatchParticipant { Id = Guid.NewGuid(), TenantId = tenantId, MatchId = matchId, ParticipantListMemberId = null, OwnLastName = "Walkin", OwnFullName = "Walk In" }
            ]
        };
        db.AddRange(new Tenant { Id = tenantId, Name = "Tenant" }, new ParticipantList { Id = listId, TenantId = tenantId, Name = "List" }, match);
        await db.SaveChangesAsync();
        var scoring = new ScoringService();
        var personalBestContext = new PersonalBestContext(db);
        var personalBestEngine = new PersonalBestEngine(db);
        var controller = new MatchesController(db, new TestTenantContext(tenantId), scoring, new LiveScoringService(scoring), new PersonalBestRegistrationService(db, personalBestContext, personalBestEngine), new PersonalBestLiveLookup(db, personalBestContext, personalBestEngine, new MemoryCache(new MemoryCacheOptions())));

        var result = Assert.IsType<OkObjectResult>(await controller.List(CancellationToken.None));
        var items = Assert.IsAssignableFrom<IReadOnlyList<MatchListItem>>(result.Value);
        var item = Assert.Single(items);

        Assert.Equal(2, item.ParticipantCount);
        Assert.Equal(1, item.UnlistedParticipantCount);
    }

    [Fact]
    public async Task ScopeConflicts_counts_other_tenants_open_matches_on_matching_scopes_only()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var tenantC = Guid.NewGuid();
        var myMatchId = Guid.NewGuid();

        db.AddRange(
            new Tenant { Id = tenantA, Name = "Mine" },
            new Tenant { Id = tenantB, Name = "Other Club" },
            new Tenant { Id = tenantC, Name = "Unrelated" },
            new Match { Id = myMatchId, TenantId = tenantA, Name = "Mine", IsOpen = true, LiveScopes = [Scope(tenantA, myMatchId, "centaurhal")] },
            MatchWithScopes(tenantA, "Mine other", isOpen: true, scopes: ["centaurhal"]),
            MatchWithScopes(tenantB, "B1", isOpen: true, scopes: ["centaurhal", "all"]),
            MatchWithScopes(tenantB, "B2", isOpen: true, scopes: ["centaurhal"]),
            MatchWithScopes(tenantC, "C1 closed", isOpen: false, scopes: ["centaurhal"]));
        await db.SaveChangesAsync();
        var controller = NewController(db, tenantA);

        var result = Assert.IsType<OkObjectResult>(await controller.ScopeConflicts(myMatchId, CancellationToken.None));
        var conflicts = Assert.IsAssignableFrom<IReadOnlyList<ScopeConflictSummary>>(result.Value);

        var conflict = Assert.Single(conflicts);
        Assert.Equal(tenantB, conflict.TenantId);
        Assert.Equal("Other Club", conflict.TenantName);
        Assert.Equal("centaurhal", conflict.Scope);
        Assert.Equal(2, conflict.MatchCount);
    }

    [Fact]
    public async Task ClaimScope_deactivates_only_other_tenants_matches_sharing_a_scope()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var myMatchId = Guid.NewGuid();
        var mySiblingMatchId = Guid.NewGuid();
        var otherMatch1 = MatchWithScopes(tenantB, "B1", isOpen: true, scopes: ["centaurhal"]);
        var otherMatch2 = MatchWithScopes(tenantB, "B2 different scope", isOpen: true, scopes: ["all"]);

        db.AddRange(
            new Tenant { Id = tenantA, Name = "Mine" },
            new Tenant { Id = tenantB, Name = "Other Club" },
            new Match { Id = myMatchId, TenantId = tenantA, Name = "Mine", IsOpen = true, LiveScopes = [Scope(tenantA, myMatchId, "centaurhal")] },
            new Match { Id = mySiblingMatchId, TenantId = tenantA, Name = "Mine sibling", IsOpen = true, LiveScopes = [Scope(tenantA, mySiblingMatchId, "centaurhal")] },
            otherMatch1,
            otherMatch2);
        await db.SaveChangesAsync();
        var controller = NewController(db, tenantA);

        Assert.IsType<NoContentResult>(await controller.ClaimScope(myMatchId, CancellationToken.None));

        Assert.False((await db.Matches.SingleAsync(item => item.Id == otherMatch1.Id)).IsOpen);
        Assert.True((await db.Matches.SingleAsync(item => item.Id == otherMatch2.Id)).IsOpen); // different scope, untouched
        Assert.True((await db.Matches.SingleAsync(item => item.Id == mySiblingMatchId)).IsOpen); // same tenant, untouched
    }

    [Fact]
    public async Task ClaimScope_registers_personal_best_for_the_other_tenants_match_it_closes()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var myMatchId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        var otherMatchId = Guid.NewGuid();
        var otherParticipantId = Guid.NewGuid();
        var otherListId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();

        db.AddRange(
            new Tenant { Id = tenantA, Name = "Mine" },
            new Tenant { Id = tenantB, Name = "Other Club", PersonalBestEnabled = true },
            new ParticipantList { Id = otherListId, TenantId = tenantB, Name = "List" },
            new Match { Id = myMatchId, TenantId = tenantA, Name = "Mine", IsOpen = true, LiveScopes = [Scope(tenantA, myMatchId, "centaurhal")] },
            new PersonalBestClassifier { Id = Guid.NewGuid(), TenantId = tenantB, Name = "Outdoor" },
            new PersonalBestDiscipline
            {
                Id = disciplineId,
                TenantId = tenantB,
                Name = "Recurve",
                Mappings = [new PersonalBestDisciplineMapping { Id = Guid.NewGuid(), TenantId = tenantB, DisciplineId = disciplineId, SourceTenantId = tenantB, CategoryId = categoryId, ValueId = 1 }]
            },
            new Match
            {
                Id = otherMatchId,
                TenantId = tenantB,
                Name = "Other Club's forgotten match",
                IsOpen = true,
                Date = new DateOnly(2026, 8, 28),
                Ends = 10,
                ArrowsPerEnd = 3,
                PersonalBestClassifier = "Outdoor",
                LiveScopes = [Scope(tenantB, otherMatchId, "centaurhal")],
                Participants =
                [
                    new MatchParticipant
                    {
                        Id = otherParticipantId,
                        TenantId = tenantB,
                        MatchId = otherMatchId,
                        ParticipantListMemberId = otherMemberId,
                        ParticipantListMember = new ParticipantListMember { Id = otherMemberId, TenantId = tenantB, ParticipantListId = otherListId, LastName = "Archer", FullName = "Robin Archer", FederationNumber = "42", Categories = new Dictionary<Guid, int> { [categoryId] = 1 } },
                        Scores = [new ArrowScore { Id = Guid.NewGuid(), TenantId = tenantB, MatchParticipantId = otherParticipantId, End = 1, Arrow = 1, KeyId = "10", Value = 10 }]
                    }
                ]
            });
        await db.SaveChangesAsync();
        var controller = NewController(db, tenantA);

        Assert.IsType<NoContentResult>(await controller.ClaimScope(myMatchId, CancellationToken.None));

        Assert.False((await db.Matches.SingleAsync(item => item.Id == otherMatchId)).IsOpen);
        var entry = Assert.Single(db.PersonalBestLogEntries);
        Assert.Equal("42", entry.FederationNumber);
        Assert.Equal("Recurve", entry.Discipline);
        Assert.Equal("Outdoor", entry.MatchClassifier);
        Assert.Equal(10, entry.Score);
    }

    [Fact]
    public async Task ClaimScope_rejects_when_the_callers_own_match_is_not_open()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantA = Guid.NewGuid();
        var myMatchId = Guid.NewGuid();
        db.AddRange(
            new Tenant { Id = tenantA, Name = "Mine" },
            new Match { Id = myMatchId, TenantId = tenantA, Name = "Mine", IsOpen = false, LiveScopes = [Scope(tenantA, myMatchId, "centaurhal")] });
        await db.SaveChangesAsync();
        var controller = NewController(db, tenantA);

        var result = Assert.IsType<ConflictObjectResult>(await controller.ClaimScope(myMatchId, CancellationToken.None));
        var error = Assert.IsType<ApiError>(result.Value);
        Assert.Equal("MATCH_NOT_OPEN", error.Code);
    }

    [Fact]
    public async Task ScopeConflicts_and_ClaimScope_are_forbidden_without_manage_rights()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantA = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        db.AddRange(new Tenant { Id = tenantA, Name = "Mine" }, new Match { Id = matchId, TenantId = tenantA, Name = "Mine", IsOpen = true });
        await db.SaveChangesAsync();
        var controller = NewController(db, tenantA, canManage: false);

        Assert.IsType<ForbidResult>(await controller.ScopeConflicts(matchId, CancellationToken.None));
        Assert.IsType<ForbidResult>(await controller.ClaimScope(matchId, CancellationToken.None));
    }

    [Fact]
    public async Task Editing_a_participant_list_member_propagates_immediately_into_an_existing_match_without_re_adding_them()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using (var setupDb = new ApplicationDbContext(options))
            await setupDb.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        await using (var seedDb = new ApplicationDbContext(options))
        {
            seedDb.AddRange(
                new Tenant { Id = tenantId, Name = "Tenant" },
                new ParticipantList { Id = listId, TenantId = tenantId, Name = "Club members", Members = [new ParticipantListMember { Id = memberId, TenantId = tenantId, LastName = "Archer", FullName = "Old Name" }] },
                new Match { Id = matchId, TenantId = tenantId, Name = "Open", ParticipantListId = listId, ArrowsPerEnd = 3, Participants = [new MatchParticipant { Id = participantId, TenantId = tenantId, MatchId = matchId, ParticipantListMemberId = memberId }] });
            await seedDb.SaveChangesAsync();
        }

        // Before the edit, the match already shows the roster member's current name.
        await using (var readDb = new ApplicationDbContext(options))
        {
            var before = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<MatchParticipant>>(Assert.IsType<OkObjectResult>(await NewController(readDb, tenantId).Participants(matchId, CancellationToken.None)).Value));
            Assert.Equal("Old Name", before.FullName);
        }

        // Edit the roster entry via a separate DbContext/request, as ParticipantListsController.UpdateMember would.
        await using (var editDb = new ApplicationDbContext(options))
        {
            var editController = new ParticipantListsController(editDb, new TestTenantContext(tenantId), new ParticipantListExcelService());
            var request = new CreateParticipantRequest("Archer", "New Name", null, [], true);
            Assert.IsType<OkObjectResult>(await editController.UpdateMember(listId, memberId, request, CancellationToken.None));
        }

        // A fresh request against the match reflects the edit immediately - no re-adding the participant.
        await using (var verifyDb = new ApplicationDbContext(options))
        {
            var controller = NewController(verifyDb, tenantId);
            var participant = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<MatchParticipant>>(Assert.IsType<OkObjectResult>(await controller.Participants(matchId, CancellationToken.None)).Value));
            Assert.Equal("New Name", participant.FullName);

            var results = Assert.IsAssignableFrom<IReadOnlyList<ParticipantResult>>(Assert.IsType<OkObjectResult>(await controller.Results(matchId, CancellationToken.None)).Value);
            Assert.Equal("New Name", Assert.Single(results).Name);
        }
    }

    private static Match MatchWithScopes(Guid tenantId, string name, bool isOpen, IEnumerable<string> scopes)
    {
        var matchId = Guid.NewGuid();
        return new Match { Id = matchId, TenantId = tenantId, Name = name, IsOpen = isOpen, LiveScopes = scopes.Select(scope => Scope(tenantId, matchId, scope)).ToList() };
    }

    private static LiveScoreScope Scope(Guid tenantId, Guid matchId, string scope) => new() { Id = Guid.NewGuid(), TenantId = tenantId, MatchId = matchId, Scope = scope };

    private static MatchesController NewController(ApplicationDbContext db, Guid tenantId, bool canManage = true)
    {
        var scoring = new ScoringService();
        var personalBestContext = new PersonalBestContext(db);
        var personalBestEngine = new PersonalBestEngine(db);
        return new MatchesController(db, new TestTenantContext(tenantId, canManage), scoring, new LiveScoringService(scoring), new PersonalBestRegistrationService(db, personalBestContext, personalBestEngine), new PersonalBestLiveLookup(db, personalBestContext, personalBestEngine, new MemoryCache(new MemoryCacheOptions())));
    }

    private static ArrowScore Score(Guid tenantId, Guid participantId, int end, int arrow, string keyId, int value) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        MatchParticipantId = participantId,
        End = end,
        Arrow = arrow,
        KeyId = keyId,
        Value = value
    };

    private sealed class TestTenantContext(Guid tenantId, bool canManage = true) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid AccountId { get; } = Guid.NewGuid();
        public bool IsAdministrator => true;
        public bool CanManage => canManage;
        public DateTime TokenExpiresAtUtc => DateTime.UtcNow.AddHours(4);
    }
}