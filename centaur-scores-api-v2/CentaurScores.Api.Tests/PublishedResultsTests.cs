using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Controllers;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace CentaurScores.Api.Tests;

public sealed class PublishedResultsTests
{
    [Theory]
    [InlineData("Club Champs 2026!", "club-champs-2026")]
    [InlineData("  --Één   Twee--  ", "een-twee")]
    [InlineData("a/b_c.d", "a-b-c-d")]
    [InlineData("!!!", "")]
    [InlineData(null, "")]
    public void Normalize_produces_lowercase_dash_separated_slugs(string? input, string expected)
    {
        Assert.Equal(expected, PublishedSlugs.Normalize(input));
    }

    [Fact]
    public void Normalize_truncates_to_the_maximum_length_without_a_trailing_dash()
    {
        var slug = PublishedSlugs.Normalize(new string('a', PublishedSlugs.MaxLength - 1) + " bcd");
        Assert.True(slug.Length <= PublishedSlugs.MaxLength);
        Assert.False(slug.EndsWith('-'));
    }

    [Fact]
    public async Task SetPublication_normalizes_rejects_taken_and_invalid_slugs_and_can_unpublish()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var match = new Match { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Indoor" };
        // A competition of another tenant already owns the slug: slugs are one global namespace.
        var competition = new Competition { Id = Guid.NewGuid(), TenantId = otherTenantId, Name = "Cup", PublishedSlug = "winter-cup" };
        db.AddRange(new Tenant { Id = tenantId, Name = "A" }, new Tenant { Id = otherTenantId, Name = "B" }, match, competition);
        await db.SaveChangesAsync();
        var controller = CreateMatchesController(db, tenantId);

        var taken = await controller.SetPublication(match.Id, new UpdatePublicationRequest(true, "Winter Cup"), CancellationToken.None);
        Assert.Equal(PublishedSlugs.TakenCode, Assert.IsType<ApiError>(Assert.IsType<ConflictObjectResult>(taken).Value).Code);

        var invalid = await controller.SetPublication(match.Id, new UpdatePublicationRequest(true, " ?! "), CancellationToken.None);
        Assert.Equal(PublishedSlugs.InvalidCode, Assert.IsType<ApiError>(Assert.IsType<BadRequestObjectResult>(invalid).Value).Code);
        Assert.Null((await db.Matches.AsNoTracking().SingleAsync(item => item.Id == match.Id)).PublishedSlug);

        var published = await controller.SetPublication(match.Id, new UpdatePublicationRequest(true, "Indoor Finals!", false), CancellationToken.None);
        Assert.Equal(new PublicationResponse("indoor-finals", false), Assert.IsType<OkObjectResult>(published).Value);
        Assert.False((await db.Matches.AsNoTracking().SingleAsync(item => item.Id == match.Id)).PublishedIncludesActiveMatches);
        // Saving the same slug again for the same match is not a conflict with itself.
        Assert.IsType<OkObjectResult>(await controller.SetPublication(match.Id, new UpdatePublicationRequest(true, "indoor-finals"), CancellationToken.None));

        var unpublished = await controller.SetPublication(match.Id, new UpdatePublicationRequest(false, "indoor-finals"), CancellationToken.None);
        Assert.Null(Assert.IsType<PublicationResponse>(Assert.IsType<OkObjectResult>(unpublished).Value).PublishedSlug);
        Assert.Null((await db.Matches.AsNoTracking().SingleAsync(item => item.Id == match.Id)).PublishedSlug);
    }

    [Fact]
    public async Task Published_match_lists_everyone_ungrouped_and_merges_profiles_with_the_same_grouping()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var tenantId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var match = new Match
        {
            Id = matchId,
            TenantId = tenantId,
            Name = "Indoor",
            Date = new DateOnly(2026, 11, 8),
            Ends = 1,
            ArrowsPerEnd = 3,
            PublishedSlug = "indoor",
            Participants =
            [
                Participant(tenantId, matchId, "Senior One", "111", classId, 1, 9, 9, 9),
                Participant(tenantId, matchId, "Senior Two", "222", classId, 1, 5, 5, 5),
                Participant(tenantId, matchId, "Junior", null, classId, 2, 10, 10, 1)
            ],
            LiveScopes =
            [
                Scope(tenantId, matchId, "hall", [classId]),
                Scope(tenantId, matchId, "foyer", [classId]),
                Scope(tenantId, matchId, "all", [])
            ]
        };
        db.AddRange(
            new Tenant { Id = tenantId, Name = "Club" },
            new Category { Id = classId, TenantId = tenantId, Name = "Class", Values = [Value(tenantId, classId, 1, "Senior"), Value(tenantId, classId, 2, "Junior")] },
            match);
        await db.SaveChangesAsync();

        var results = await CreateService(db).FindAsync("Indoor", CancellationToken.None);

        Assert.NotNull(results);
        Assert.Equal("match", results.Kind);
        Assert.Null(results.Competition);
        var published = Assert.IsType<PublishedMatchResults>(results.Match);
        Assert.Collection(
            published.Ungrouped,
            entry => { Assert.Equal((1, "Senior One", "111", 27, 3), (entry.Position, entry.Name, entry.FederationNumber, entry.Score, entry.Arrows)); Assert.Equal(9, entry.Average); },
            entry => Assert.Equal((2, "Junior"), (entry.Position, entry.Name)),
            entry => Assert.Equal((3, "Senior Two"), (entry.Position, entry.Name)));
        var grouping = Assert.Single(published.Groupings);
        Assert.Equal(["Class"], grouping.CategoryNames);
        Assert.Collection(
            grouping.Blocks,
            block => { Assert.Equal("Junior", block.Name); Assert.Equal(1, Assert.Single(block.Entries).Position); },
            block => { Assert.Equal("Senior", block.Name); Assert.Equal(["Senior One", "Senior Two"], block.Entries.Select(item => item.Name)); });

        Assert.Null(await CreateService(db).FindAsync("unknown", CancellationToken.None));
    }

    [Fact]
    public async Task Published_competition_shows_counting_scores_f1_points_absent_rounds_and_averages()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var tenantId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var memberA = new ParticipantListMember { Id = Guid.NewGuid(), TenantId = tenantId, ParticipantListId = listId, FullName = "A Archer", LastName = "Archer", FederationNumber = "100" };
        var memberB = new ParticipantListMember { Id = Guid.NewGuid(), TenantId = tenantId, ParticipantListId = listId, FullName = "B Bowman", LastName = "Bowman", FederationNumber = "200" };
        var round1 = new CompetitionRound { Id = Guid.NewGuid(), TenantId = tenantId, Order = 0, ShortName = "R1", LongName = "Round 1" };
        var round2 = new CompetitionRound { Id = Guid.NewGuid(), TenantId = tenantId, Order = 1, ShortName = "R2", LongName = "Round 2" };
        var round3 = new CompetitionRound { Id = Guid.NewGuid(), TenantId = tenantId, Order = 2, ShortName = "R3", LongName = "Round 3" };
        // R1: A 27 (12 pts), B 18 (10 pts). R2: A 15 (10 pts), B 21 (12 pts). R3: only B, 3 (12 pts).
        var match1 = MemberMatch(tenantId, new DateOnly(2026, 10, 1), (memberA, [9, 9, 9]), (memberB, [6, 6, 6]));
        var match2 = MemberMatch(tenantId, new DateOnly(2026, 10, 8), (memberA, [5, 5, 5]), (memberB, [7, 7, 7]));
        var match3 = MemberMatch(tenantId, new DateOnly(2026, 10, 15), (memberB, [1, 1, 1]));
        var competitionId = Guid.NewGuid();
        round1.Matches.Add(new CompetitionRoundMatch { Id = Guid.NewGuid(), TenantId = tenantId, CompetitionRoundId = round1.Id, MatchId = match1.Id });
        round2.Matches.Add(new CompetitionRoundMatch { Id = Guid.NewGuid(), TenantId = tenantId, CompetitionRoundId = round2.Id, MatchId = match2.Id });
        round3.Matches.Add(new CompetitionRoundMatch { Id = Guid.NewGuid(), TenantId = tenantId, CompetitionRoundId = round3.Id, MatchId = match3.Id });
        var competition = new Competition
        {
            Id = competitionId,
            TenantId = tenantId,
            Name = "Winter Cup",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 10, 31),
            PublishedSlug = "winter-cup",
            Rounds = [round1, round2, round3],
            ScoringRules = [new CompetitionScoreRule { Id = Guid.NewGuid(), TenantId = tenantId, Name = "F1", RoundIdsJson = JsonSerializer.Serialize(new[] { round1.Id, round2.Id, round3.Id }), HighestScores = 1, MinimumScores = 1, Aggregation = "f1" }]
        };
        db.AddRange(
            new Tenant { Id = tenantId, Name = "Club", LogoUrl = "https://example.com/logo.png" },
            new ParticipantList { Id = listId, TenantId = tenantId, Name = "Members", Members = [memberA, memberB] },
            match1, match2, match3, competition);
        await db.SaveChangesAsync();

        var results = await CreateService(db).FindAsync("winter-cup", CancellationToken.None);

        Assert.NotNull(results);
        Assert.Equal(("competition", "Club", "https://example.com/logo.png"), (results.Kind, results.Tenant, results.Logo));
        var published = Assert.IsType<PublishedCompetitionResults>(results.Competition);
        Assert.True(published.UsesF1);
        Assert.False(published.IsGrouped);
        Assert.Equal(["R1", "R2", "R3"], published.Rounds.Select(item => item.ShortName));
        var entryA = Assert.Single(Assert.Single(published.Groups).Entries, item => item.Name == "A Archer");
        Assert.Equal("100", entryA.FederationNumber);
        Assert.Equal((12, 27, (int?)12), (entryA.Total, entryA.MatchScoreTotal, entryA.F1Points));
        Assert.Equal(9, entryA.Average);
        Assert.Collection(
            entryA.Rounds,
            score => Assert.Equal(((int?)27, (int?)12, true), (score.Score, score.F1Points, score.Counts)),
            score => Assert.Equal(((int?)15, (int?)10, false), (score.Score, score.F1Points, score.Counts)),
            score => Assert.Equal(((int?)null, (int?)null, false), (score.Score, score.F1Points, score.Counts)));
        // The ungrouped list shows the same per-archer figures as the grouped one.
        Assert.Equal(2, published.Ungrouped.Count);
        var ungroupedA = Assert.Single(published.Ungrouped, item => item.Name == "A Archer");
        Assert.Equal(entryA.Rounds, ungroupedA.Rounds);
        Assert.Equal((entryA.Total, entryA.F1Points), (ungroupedA.Total, ungroupedA.F1Points));
    }

    [Fact]
    public async Task Active_match_is_shown_provisionally_or_held_back_until_it_closes()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var tenantId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var match = new Match { Id = matchId, TenantId = tenantId, Name = "Live", IsOpen = true, ArrowsPerEnd = 3, PublishedSlug = "live", Participants = [Participant(tenantId, matchId, "Archer", null, Guid.NewGuid(), 1, 9, 9, 9)] };
        db.AddRange(new Tenant { Id = tenantId, Name = "Club" }, match);
        await db.SaveChangesAsync();

        var included = await CreateService(db).FindAsync("live", CancellationToken.None);
        Assert.NotNull(included);
        Assert.Equal((true, 1), (included.IncludesActiveMatches, included.ActiveMatchCount));
        Assert.Single(Assert.IsType<PublishedMatchResults>(included.Match).Ungrouped);

        match.PublishedIncludesActiveMatches = false;
        await db.SaveChangesAsync();
        var heldBack = await CreateService(db).FindAsync("live", CancellationToken.None);
        Assert.NotNull(heldBack);
        Assert.Equal((false, 1), (heldBack.IncludesActiveMatches, heldBack.ActiveMatchCount));
        Assert.Null(heldBack.Match);

        match.IsOpen = false;
        await db.SaveChangesAsync();
        var closed = await CreateService(db).FindAsync("live", CancellationToken.None);
        Assert.NotNull(closed);
        Assert.Equal(0, closed.ActiveMatchCount);
        Assert.NotNull(closed.Match);
    }

    [Fact]
    public async Task Competition_can_leave_active_matches_out_until_they_close()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var tenantId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var member = new ParticipantListMember { Id = Guid.NewGuid(), TenantId = tenantId, ParticipantListId = listId, FullName = "A Archer", LastName = "Archer" };
        var finished = MemberMatch(tenantId, new DateOnly(2026, 10, 1), (member, [9, 9, 9]));
        var active = MemberMatch(tenantId, new DateOnly(2026, 10, 8), (member, [5, 5, 5]));
        active.IsOpen = true;
        var round1 = new CompetitionRound { Id = Guid.NewGuid(), TenantId = tenantId, Order = 0, ShortName = "R1", LongName = "Round 1" };
        var round2 = new CompetitionRound { Id = Guid.NewGuid(), TenantId = tenantId, Order = 1, ShortName = "R2", LongName = "Round 2" };
        round1.Matches.Add(new CompetitionRoundMatch { Id = Guid.NewGuid(), TenantId = tenantId, CompetitionRoundId = round1.Id, MatchId = finished.Id });
        round2.Matches.Add(new CompetitionRoundMatch { Id = Guid.NewGuid(), TenantId = tenantId, CompetitionRoundId = round2.Id, MatchId = active.Id });
        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Cup",
            PublishedSlug = "cup",
            PublishedIncludesActiveMatches = false,
            Rounds = [round1, round2],
            ScoringRules = [new CompetitionScoreRule { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Total", RoundIdsJson = JsonSerializer.Serialize(new[] { round1.Id, round2.Id }), HighestScores = 2, MinimumScores = 0, Aggregation = "total" }]
        };
        db.AddRange(new Tenant { Id = tenantId, Name = "Club" }, new ParticipantList { Id = listId, TenantId = tenantId, Name = "Members", Members = [member] }, finished, active, competition);
        await db.SaveChangesAsync();

        var excluded = await CreateService(db).FindAsync("cup", CancellationToken.None);
        Assert.NotNull(excluded);
        Assert.Equal((false, 1), (excluded.IncludesActiveMatches, excluded.ActiveMatchCount));
        var entry = Assert.Single(Assert.IsType<PublishedCompetitionResults>(excluded.Competition).Ungrouped);
        Assert.Equal(27, entry.Total);
        Assert.Null(entry.Rounds[1].Score);

        competition.PublishedIncludesActiveMatches = true;
        await db.SaveChangesAsync();
        var included = await CreateService(db).FindAsync("cup", CancellationToken.None);
        Assert.NotNull(included);
        Assert.Equal(42, Assert.Single(Assert.IsType<PublishedCompetitionResults>(included.Competition).Ungrouped).Total);
    }

    [Fact]
    public void Ungrouped_competition_is_ordered_by_average_then_federation_number_then_name()
    {
        static CompetitionExportParticipant Person(string name, string? federationNumber, int countingScore, int countingArrows, bool qualified = true) => new(
            Guid.NewGuid(), name, name, federationNumber, new Dictionary<Guid, int>(), "Results", "1", qualified, countingScore,
            new Dictionary<Guid, CompetitionExportRoundScore>(), [], new HashSet<Guid>(), countingScore, countingArrows, []);
        var data = new CompetitionExportData(
            new Competition { Id = Guid.NewGuid(), Name = "Cup" },
            [],
            [],
            [
                Person("Zed", "300", 270, 30),
                Person("Amy", "20", 270, 30),
                Person("Bob", null, 270, 30),
                Person("Cid", "20", 270, 30),
                Person("Dan", "1", 280, 30, qualified: false),
                Person("Eve", "5", 240, 30),
                Person("Fay", "6", 0, 0)
            ]);

        var results = CreateService(null!).BuildCompetition(data);

        Assert.Equal(
            [("-", "Dan"), ("1", "Amy"), ("1", "Cid"), ("1", "Zed"), ("1", "Bob"), ("5", "Eve"), ("6", "Fay")],
            results.Ungrouped.Select(item => (item.Position, item.Name)));
    }

    private static async Task<ApplicationDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static PublishedResultsService CreateService(ApplicationDbContext db)
    {
        var scoring = new ScoringService();
        return new PublishedResultsService(db, new LiveScoringService(scoring), new CompetitionService(scoring));
    }

    private static MatchesController CreateMatchesController(ApplicationDbContext db, Guid tenantId)
    {
        var scoring = new ScoringService();
        var personalBestContext = new PersonalBestContext(db);
        var personalBestEngine = new PersonalBestEngine(db);
        return new MatchesController(db, new TestTenantContext(tenantId), scoring, new LiveScoringService(scoring), new PersonalBestRegistrationService(db, personalBestContext, personalBestEngine), new PersonalBestLiveLookup(db, personalBestContext, personalBestEngine, new MemoryCache(new MemoryCacheOptions())));
    }

    private static MatchParticipant Participant(Guid tenantId, Guid matchId, string name, string? federationNumber, Guid categoryId, int valueId, params int[] arrows)
    {
        var participantId = Guid.NewGuid();
        return new MatchParticipant
        {
            Id = participantId,
            TenantId = tenantId,
            MatchId = matchId,
            OwnFullName = name,
            OwnLastName = name,
            OwnFederationNumber = federationNumber,
            OwnCategories = new Dictionary<Guid, int> { [categoryId] = valueId },
            Scores = Arrows(tenantId, participantId, arrows)
        };
    }

    private static Match MemberMatch(Guid tenantId, DateOnly date, params (ParticipantListMember Member, int[] Arrows)[] shooters)
    {
        var matchId = Guid.NewGuid();
        return new Match
        {
            Id = matchId,
            TenantId = tenantId,
            Name = $"Match {date}",
            Date = date,
            Ends = 1,
            ArrowsPerEnd = 3,
            Participants = shooters.Select(shooter =>
            {
                var participantId = Guid.NewGuid();
                return new MatchParticipant { Id = participantId, TenantId = tenantId, MatchId = matchId, ParticipantListMemberId = shooter.Member.Id, Scores = Arrows(tenantId, participantId, shooter.Arrows) };
            }).ToList()
        };
    }

    private static List<ArrowScore> Arrows(Guid tenantId, Guid participantId, int[] arrows) =>
        arrows.Select((value, index) => new ArrowScore { Id = Guid.NewGuid(), TenantId = tenantId, MatchParticipantId = participantId, End = 1, Arrow = index + 1, KeyId = value.ToString(), Value = value }).ToList();

    private static LiveScoreScope Scope(Guid tenantId, Guid matchId, string scope, Guid[] groupBy) =>
        new() { Id = Guid.NewGuid(), TenantId = tenantId, MatchId = matchId, Scope = scope, GroupByCategoryIdsJson = JsonSerializer.Serialize(groupBy) };

    private static CategoryValue Value(Guid tenantId, Guid categoryId, int valueId, string name) =>
        new() { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = categoryId, ValueId = valueId, Name = name };

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid AccountId { get; } = Guid.NewGuid();
        public bool IsAdministrator => false;
        public bool CanManage => true;
        public DateTime TokenExpiresAtUtc => DateTime.UtcNow.AddHours(4);
    }
}

public sealed class PublicRateLimitingTests
{
    [Theory]
    // Direct internet callers: the header is ignored, so a spoofed X-Forwarded-For cannot dodge the limit.
    [InlineData("203.0.113.7", "198.51.100.1", "203.0.113.7")]
    // Behind our proxy: the rightmost entry (appended by nginx) is the client; earlier client-supplied entries are ignored.
    [InlineData("172.18.0.3", "1.2.3.4, 198.51.100.9", "198.51.100.9")]
    [InlineData("127.0.0.1", "198.51.100.9", "198.51.100.9")]
    [InlineData("127.0.0.1", "", "127.0.0.1")]
    [InlineData("::ffff:192.168.1.5", "garbage", "192.168.1.5")]
    // IPv6 clients share one bucket per /64.
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "", "2001:db8:1:2::/64")]
    public void ClientAddress_trusts_forwarded_for_only_from_internal_proxies(string remote, string forwardedFor, string expected)
    {
        Assert.Equal(expected, PublicRateLimiting.ClientAddress(System.Net.IPAddress.Parse(remote), forwardedFor));
    }
}
