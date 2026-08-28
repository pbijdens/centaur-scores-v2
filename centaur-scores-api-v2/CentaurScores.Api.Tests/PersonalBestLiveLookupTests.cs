using CentaurScores.Api.Application;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CentaurScores.Api.Tests;

public sealed class PersonalBestLiveLookupTests
{
    [Fact]
    public async Task BuildAsync_computes_the_current_best_and_then_serves_it_from_cache_without_requerying()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();

        var match = new Match
        {
            Id = matchId,
            TenantId = tenantId,
            Name = "Open",
            Ends = 5,
            ArrowsPerEnd = 6,
            PersonalBestClassifier = "Outdoor",
            Participants = [new MatchParticipant { Id = participantId, TenantId = tenantId, MatchId = matchId, FederationNumber = "123", FullName = "Robin Archer", LastName = "Archer", Categories = new Dictionary<Guid, int> { [categoryId] = 7 } }]
        };
        db.AddRange(
            new Tenant { Id = tenantId, Name = "Tenant", PersonalBestEnabled = true },
            new PersonalBestDiscipline { Id = disciplineId, TenantId = tenantId, Name = "Recurve" },
            new PersonalBestDisciplineMapping { Id = Guid.NewGuid(), TenantId = tenantId, DisciplineId = disciplineId, SourceTenantId = tenantId, CategoryId = categoryId, ValueId = 7 },
            new PersonalBestLogEntry { Id = Guid.NewGuid(), TenantId = tenantId, FederationNumber = "123", Discipline = "Recurve", MatchClassifier = "Outdoor", Score = 270, Date = new DateOnly(2026, 1, 1), RecordedAt = DateTime.UtcNow },
            match);
        await db.SaveChangesAsync();

        var scope = new LiveScoreScope { Id = Guid.NewGuid(), TenantId = tenantId, MatchId = matchId, IncludePersonalBest = true };
        var lookup = new PersonalBestLiveLookup(db, new PersonalBestContext(db), new PersonalBestEngine(db), new MemoryCache(new MemoryCacheOptions()));

        var first = await lookup.BuildAsync(match, scope, CancellationToken.None);
        Assert.Equal(270.0 / 30, Assert.Contains(participantId, first));

        // A higher score is registered elsewhere while the match is live; the cached value should not change.
        db.PersonalBestLogEntries.Add(new PersonalBestLogEntry { Id = Guid.NewGuid(), TenantId = tenantId, FederationNumber = "123", Discipline = "Recurve", MatchClassifier = "Outdoor", Score = 300, Date = new DateOnly(2026, 2, 1), RecordedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var second = await lookup.BuildAsync(match, scope, CancellationToken.None);
        Assert.Equal(270.0 / 30, Assert.Contains(participantId, second));
    }

    [Fact]
    public async Task Invalidate_forces_the_next_BuildAsync_to_pick_up_a_participant_change()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var tenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();

        var match = new Match { Id = matchId, TenantId = tenantId, Name = "Open", Ends = 5, ArrowsPerEnd = 6, PersonalBestClassifier = "Outdoor" };
        db.AddRange(
            new Tenant { Id = tenantId, Name = "Tenant", PersonalBestEnabled = true },
            new PersonalBestDiscipline { Id = disciplineId, TenantId = tenantId, Name = "Recurve" },
            new PersonalBestDisciplineMapping { Id = Guid.NewGuid(), TenantId = tenantId, DisciplineId = disciplineId, SourceTenantId = tenantId, CategoryId = categoryId, ValueId = 7 },
            new PersonalBestLogEntry { Id = Guid.NewGuid(), TenantId = tenantId, FederationNumber = "123", Discipline = "Recurve", MatchClassifier = "Outdoor", Score = 270, Date = new DateOnly(2026, 1, 1), RecordedAt = DateTime.UtcNow },
            match);
        await db.SaveChangesAsync();

        var scope = new LiveScoreScope { Id = Guid.NewGuid(), TenantId = tenantId, MatchId = matchId, IncludePersonalBest = true };
        var lookup = new PersonalBestLiveLookup(db, new PersonalBestContext(db), new PersonalBestEngine(db), new MemoryCache(new MemoryCacheOptions()));

        var beforeJoining = await lookup.BuildAsync(match, scope, CancellationToken.None);
        Assert.Empty(beforeJoining);

        // A new participant joins the match after the cache was already populated.
        var newParticipantId = Guid.NewGuid();
        match.Participants.Add(new MatchParticipant { Id = newParticipantId, TenantId = tenantId, MatchId = matchId, FederationNumber = "123", FullName = "Robin Archer", LastName = "Archer", Categories = new Dictionary<Guid, int> { [categoryId] = 7 } });

        var stillCached = await lookup.BuildAsync(match, scope, CancellationToken.None);
        Assert.Empty(stillCached);

        lookup.Invalidate(matchId);

        var afterInvalidate = await lookup.BuildAsync(match, scope, CancellationToken.None);
        Assert.Equal(270.0 / 30, Assert.Contains(newParticipantId, afterInvalidate));
    }
}
