using CentaurScores.Api.Application;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Tests;

public sealed class PersonalBestRegistrationServiceTests
{
    private static async Task<ApplicationDbContext> NewDbAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static PersonalBestRegistrationService NewService(ApplicationDbContext db) =>
        new(db, new PersonalBestContext(db), new PersonalBestEngine(db));

    [Fact]
    public async Task RegisterOnDeactivationAsync_registers_a_listed_participant_with_a_mapped_discipline()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);

        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Club", PersonalBestEnabled = true };
        var categoryId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        db.Tenants.Add(tenant);
        db.PersonalBestClassifiers.Add(new PersonalBestClassifier { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Outdoor" });
        db.PersonalBestDisciplines.Add(new PersonalBestDiscipline
        {
            Id = disciplineId,
            TenantId = tenant.Id,
            Name = "Recurve",
            Mappings = [new PersonalBestDisciplineMapping { Id = Guid.NewGuid(), TenantId = tenant.Id, DisciplineId = disciplineId, SourceTenantId = tenant.Id, CategoryId = categoryId, ValueId = 1 }]
        });
        var match = new Match { Id = matchId, TenantId = tenant.Id, Date = new DateOnly(2026, 5, 1), Ends = 10, ArrowsPerEnd = 3, PersonalBestClassifier = "Outdoor" };
        db.Matches.Add(match);
        db.MatchParticipants.Add(new MatchParticipant
        {
            Id = participantId,
            TenantId = tenant.Id,
            MatchId = matchId,
            ParticipantListMemberId = Guid.NewGuid(),
            FullName = "Robin Archer",
            FederationNumber = "42",
            Categories = new Dictionary<Guid, int> { [categoryId] = 1 },
            Scores = [new ArrowScore { Id = Guid.NewGuid(), TenantId = tenant.Id, MatchParticipantId = participantId, End = 1, Arrow = 1, KeyId = "10", Value = 10 }]
        });
        await db.SaveChangesAsync();

        await NewService(db).RegisterOnDeactivationAsync(match, CancellationToken.None);

        var entry = Assert.Single(db.PersonalBestLogEntries);
        Assert.Equal("42", entry.FederationNumber);
        Assert.Equal("Recurve", entry.Discipline);
        Assert.Equal("Outdoor", entry.MatchClassifier);
        Assert.Equal(10, entry.Score);
        Assert.Equal("automatic", entry.Source);
        Assert.Equal("Robin Archer", Assert.Single(db.PersonalBestArcherNames).Name);
    }

    [Fact]
    public async Task RegisterOnDeactivationAsync_skips_unlisted_participants()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);

        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Club", PersonalBestEnabled = true };
        var categoryId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        db.Tenants.Add(tenant);
        db.PersonalBestClassifiers.Add(new PersonalBestClassifier { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Outdoor" });
        db.PersonalBestDisciplines.Add(new PersonalBestDiscipline
        {
            Id = disciplineId,
            TenantId = tenant.Id,
            Name = "Recurve",
            Mappings = [new PersonalBestDisciplineMapping { Id = Guid.NewGuid(), TenantId = tenant.Id, DisciplineId = disciplineId, SourceTenantId = tenant.Id, CategoryId = categoryId, ValueId = 1 }]
        });
        var match = new Match { Id = matchId, TenantId = tenant.Id, Date = new DateOnly(2026, 5, 1), Ends = 10, ArrowsPerEnd = 3, PersonalBestClassifier = "Outdoor" };
        db.Matches.Add(match);
        db.MatchParticipants.Add(new MatchParticipant
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            MatchId = matchId,
            ParticipantListMemberId = null, // walk-in / unlisted
            FullName = "Walk In",
            FederationNumber = "99",
            Categories = new Dictionary<Guid, int> { [categoryId] = 1 }
        });
        await db.SaveChangesAsync();

        await NewService(db).RegisterOnDeactivationAsync(match, CancellationToken.None);

        Assert.Empty(db.PersonalBestLogEntries);
    }

    [Fact]
    public async Task RegisterOnDeactivationAsync_is_a_noop_when_the_tenant_has_not_enabled_the_feature()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);

        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Club" }; // PersonalBestEnabled defaults to false
        var matchId = Guid.NewGuid();
        var match = new Match { Id = matchId, TenantId = tenant.Id, Date = new DateOnly(2026, 5, 1), Ends = 10, ArrowsPerEnd = 3, PersonalBestClassifier = "Outdoor" };
        db.AddRange(tenant, match);
        await db.SaveChangesAsync();

        await NewService(db).RegisterOnDeactivationAsync(match, CancellationToken.None);

        Assert.Empty(db.PersonalBestLogEntries);
    }

    [Fact]
    public async Task RegisterOnDeactivationAsync_is_a_noop_when_the_match_has_no_classifier()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);

        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Club", PersonalBestEnabled = true };
        var match = new Match { Id = Guid.NewGuid(), TenantId = tenant.Id, Date = new DateOnly(2026, 5, 1), Ends = 10, ArrowsPerEnd = 3, PersonalBestClassifier = null };
        db.AddRange(tenant, match);
        await db.SaveChangesAsync();

        await NewService(db).RegisterOnDeactivationAsync(match, CancellationToken.None);

        Assert.Empty(db.PersonalBestLogEntries);
    }
}
