using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Controllers;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
                    FederationNumber = "123",
                    FullName = "Robin Archer",
                    LastName = "Archer",
                    Categories = new Dictionary<Guid, int> { [disciplineId] = 7, [classId] = 2 },
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
        var controller = new MatchesController(db, new TestTenantContext(tenantId), scoring, new LiveScoringService(scoring));

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
        var listedParticipantId = Guid.NewGuid();
        var listMemberId = Guid.NewGuid();
        var match = new Match
        {
            Id = matchId,
            TenantId = tenantId,
            Name = "Open",
            Participants =
            [
                new MatchParticipant { Id = listedParticipantId, TenantId = tenantId, MatchId = matchId, ParticipantListMemberId = listMemberId, LastName = "Listed", FullName = "Listed Archer" },
                new MatchParticipant { Id = Guid.NewGuid(), TenantId = tenantId, MatchId = matchId, ParticipantListMemberId = null, LastName = "Walkin", FullName = "Walk In" }
            ]
        };
        db.AddRange(new Tenant { Id = tenantId, Name = "Tenant" }, match);
        await db.SaveChangesAsync();
        var scoring = new ScoringService();
        var controller = new MatchesController(db, new TestTenantContext(tenantId), scoring, new LiveScoringService(scoring));

        var result = Assert.IsType<OkObjectResult>(await controller.List(CancellationToken.None));
        var items = Assert.IsAssignableFrom<IReadOnlyList<MatchListItem>>(result.Value);
        var item = Assert.Single(items);

        Assert.Equal(2, item.ParticipantCount);
        Assert.Equal(1, item.UnlistedParticipantCount);
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

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid AccountId { get; } = Guid.NewGuid();
        public bool IsAdministrator => true;
        public bool CanManage => true;
    }
}