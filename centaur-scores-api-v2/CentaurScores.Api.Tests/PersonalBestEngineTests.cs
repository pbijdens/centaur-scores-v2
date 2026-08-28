using CentaurScores.Api.Application;
using CentaurScores.Api.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Tests;

public sealed class PersonalBestEngineTests
{
    private static async Task<ApplicationDbContext> NewDbAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task TryInsertAsync_inserts_when_there_are_no_prior_entries()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var engine = new PersonalBestEngine(db);
        var tenantId = Guid.NewGuid();

        var outcome = await engine.TryInsertAsync(tenantId, "123", "Recurve", "Outdoor", 500, new DateOnly(2026, 1, 1), DateTime.UtcNow, "import", CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.True(outcome.Inserted);
        Assert.False(outcome.CannotInsert);
        Assert.Single(db.PersonalBestLogEntries);
    }

    [Fact]
    public async Task TryInsertAsync_skips_an_exact_duplicate_date_and_score()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var engine = new PersonalBestEngine(db);
        var tenantId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 1);

        await engine.TryInsertAsync(tenantId, "123", "Recurve", "Outdoor", 500, date, DateTime.UtcNow, "import", CancellationToken.None);
        await db.SaveChangesAsync();

        var outcome = await engine.TryInsertAsync(tenantId, "123", "Recurve", "Outdoor", 500, date, DateTime.UtcNow, "import", CancellationToken.None);

        Assert.False(outcome.Inserted);
        Assert.False(outcome.CannotInsert);
        Assert.Single(db.PersonalBestLogEntries);
    }

    [Fact]
    public async Task TryInsertAsync_reports_a_conflict_when_a_higher_score_already_exists_on_or_before_the_date()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var engine = new PersonalBestEngine(db);
        var tenantId = Guid.NewGuid();

        await engine.TryInsertAsync(tenantId, "123", "Recurve", "Outdoor", 500, new DateOnly(2026, 1, 1), DateTime.UtcNow, "import", CancellationToken.None);
        await db.SaveChangesAsync();
        var existingId = Assert.Single(db.PersonalBestLogEntries).Id;

        var outcome = await engine.TryInsertAsync(tenantId, "123", "Recurve", "Outdoor", 490, new DateOnly(2026, 2, 1), DateTime.UtcNow, "import", CancellationToken.None);

        Assert.False(outcome.Inserted);
        Assert.True(outcome.CannotInsert);
        Assert.Equal(existingId, Assert.Single(outcome.OutrankingEntryIds));
        Assert.Single(db.PersonalBestLogEntries); // nothing was added
    }

    [Fact]
    public async Task TryInsertAsync_on_improvement_deletes_later_entries_with_a_lower_or_equal_score()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var engine = new PersonalBestEngine(db);
        var tenantId = Guid.NewGuid();

        // An earlier, lower score recorded for a later date - superseded once an even-earlier higher score arrives.
        await engine.TryInsertAsync(tenantId, "123", "Recurve", "Outdoor", 480, new DateOnly(2026, 3, 1), DateTime.UtcNow, "import", CancellationToken.None);
        await db.SaveChangesAsync();

        var outcome = await engine.TryInsertAsync(tenantId, "123", "Recurve", "Outdoor", 500, new DateOnly(2026, 1, 1), DateTime.UtcNow, "import", CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.True(outcome.Inserted);
        var remaining = Assert.Single(db.PersonalBestLogEntries);
        Assert.Equal(500, remaining.Score);
        Assert.Equal(new DateOnly(2026, 1, 1), remaining.Date);
    }

    [Fact]
    public async Task GetCurrentBestAsync_breaks_ties_on_date_by_the_highest_score()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var engine = new PersonalBestEngine(db);
        var tenantId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 1);

        db.PersonalBestLogEntries.AddRange(
            new Domain.PersonalBestLogEntry { Id = Guid.NewGuid(), TenantId = tenantId, FederationNumber = "123", Discipline = "Recurve", MatchClassifier = "Outdoor", Score = 480, Date = date, RecordedAt = DateTime.UtcNow, Source = "import" },
            new Domain.PersonalBestLogEntry { Id = Guid.NewGuid(), TenantId = tenantId, FederationNumber = "123", Discipline = "Recurve", MatchClassifier = "Outdoor", Score = 500, Date = date, RecordedAt = DateTime.UtcNow, Source = "import" });
        await db.SaveChangesAsync();

        var best = await engine.GetCurrentBestAsync(tenantId, "123", "Recurve", "Outdoor", CancellationToken.None);

        Assert.Equal(500, best?.Score);
    }

    [Fact]
    public async Task EnsureArcherNameAsync_creates_then_refreshes_the_single_name_record()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var engine = new PersonalBestEngine(db);
        var tenantId = Guid.NewGuid();

        await engine.EnsureArcherNameAsync(tenantId, "123", "Robin Archer", CancellationToken.None);
        await db.SaveChangesAsync();
        await engine.EnsureArcherNameAsync(tenantId, "123", "Robin A. Archer", CancellationToken.None);
        await db.SaveChangesAsync();

        var name = Assert.Single(db.PersonalBestArcherNames);
        Assert.Equal("Robin A. Archer", name.Name);
    }
}
