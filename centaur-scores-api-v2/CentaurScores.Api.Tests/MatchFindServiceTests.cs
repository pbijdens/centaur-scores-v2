using System.Reflection;
using System.Text.Json;
using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Controllers;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CentaurScores.Api.Tests;

public sealed class MatchFindServiceTests
{
    private static readonly DateOnly MatchDate = new(2026, 10, 22);

    [Fact]
    public async Task Finds_the_single_match_and_projects_ranked_results_with_category_properties()
    {
        await using var fixture = await Fixture.CreateAsync();
        var klasseId = fixture.AddCategory("Klasse", "klasse", (1, "Senioren"), (2, "Junioren"));
        var disciplineId = fixture.AddCategory("Discipline", null, (1, "Recurve"), (2, "Compound"));
        var unusedId = fixture.AddCategory("Unused", null, (1, "Something"));
        var listId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        fixture.Db.ParticipantLists.Add(new ParticipantList
        {
            Id = listId,
            TenantId = fixture.RootTenantId,
            Name = "Members",
            Members = [new ParticipantListMember { Id = memberId, TenantId = fixture.RootTenantId, ParticipantListId = listId, LastName = "Archer", FullName = "Amy Archer", FederationNumber = "123456", Categories = new Dictionary<Guid, int> { [klasseId] = 1 } }]
        });
        var match = fixture.AddMatch(fixture.RootTenantId, "Wintercompetitie ronde 3", "19:30");
        // List-linked participant: roster category plus a per-match override layered on top.
        fixture.AddParticipant(match, "", null, new Dictionary<Guid, int> { [disciplineId] = 1 }, memberId, 10, 9);
        // Free participant without federation number, with a value that no longer exists for "Unused".
        fixture.AddParticipant(match, "Bob Bowman", null, new Dictionary<Guid, int> { [disciplineId] = 2, [unusedId] = 99 }, null, 10, 10);
        fixture.AddParticipant(match, "Carl Nockless", "777", [], null);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.FindAsync("2026-10-22", "19:30");

        Assert.Equal(MatchFindOutcome.Found, result.Outcome);
        var found = result.Match!;
        Assert.Equal("Root Club - Wintercompetitie ronde 3", found.Description);
        Assert.Equal("2026-10-22", found.Date);
        Assert.Equal("19:30", found.ShortCode);
        Assert.Collection(found.Results,
            bob =>
            {
                Assert.Equal("Bob Bowman", bob["name"]);
                Assert.Equal(20, bob["score"]);
                Assert.Null(bob["lidnummer"]);
                Assert.Equal("Compound", bob["Discipline"]);
                Assert.False(bob.ContainsKey("klasse"));
                Assert.False(bob.ContainsKey("Unused"));
            },
            amy =>
            {
                Assert.Equal("Amy Archer", amy["name"]);
                Assert.Equal(19, amy["score"]);
                Assert.Equal("123456", amy["lidnummer"]);
                Assert.Equal("Senioren", amy["klasse"]);
                Assert.Equal("Recurve", amy["Discipline"]);
                Assert.False(amy.ContainsKey("Klasse"));
            },
            carl =>
            {
                Assert.Equal("Carl Nockless", carl["name"]);
                Assert.Equal(0, carl["score"]);
                Assert.Equal(["name", "score", "lidnummer"], carl.Keys);
            });
    }

    [Fact]
    public async Task Finds_a_match_in_a_descendant_tenant_but_never_in_an_unrelated_tenant()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.AddMatch(fixture.ChildTenantId, "Child match", "10:00");
        fixture.AddMatch(fixture.UnrelatedTenantId, "Other club match", "11:00");
        await fixture.Db.SaveChangesAsync();

        var child = await fixture.FindAsync("2026-10-22", "10:00");
        var unrelated = await fixture.FindAsync("2026-10-22", "11:00");

        Assert.Equal(MatchFindOutcome.Found, child.Outcome);
        Assert.Equal("Child Club - Child match", child.Match!.Description);
        Assert.Equal(MatchFindOutcome.NotFound, unrelated.Outcome);
        Assert.Equal("MATCH_NOT_FOUND", unrelated.Error!.Code);
    }

    [Theory]
    [InlineData("9:30", "09:30")]
    [InlineData(" 09:30 ", "9:30")]
    [InlineData("09:30", "09:30")]
    public async Task Start_time_is_compared_as_a_time_not_as_text(string shortCode, string startTime)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.AddMatch(fixture.RootTenantId, "Morning", shortCode);
        await fixture.Db.SaveChangesAsync();

        Assert.Equal(MatchFindOutcome.Found, (await fixture.FindAsync("2026-10-22", startTime)).Outcome);
    }

    [Fact]
    public async Task Wrong_date_wrong_time_and_non_time_short_codes_never_match()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.AddMatch(fixture.RootTenantId, "Morning", "09:30");
        fixture.AddMatch(fixture.RootTenantId, "Coded", "OPEN");
        fixture.AddMatch(fixture.RootTenantId, "No code", null);
        await fixture.Db.SaveChangesAsync();

        Assert.Equal(MatchFindOutcome.NotFound, (await fixture.FindAsync("2026-10-23", "09:30")).Outcome);
        Assert.Equal(MatchFindOutcome.NotFound, (await fixture.FindAsync("2026-10-22", "09:31")).Outcome);
    }

    [Fact]
    public async Task Open_and_closed_matches_are_both_found()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.AddMatch(fixture.RootTenantId, "Open match", "18:00", isOpen: true);
        fixture.AddMatch(fixture.RootTenantId, "Closed match", "20:00", isOpen: false);
        await fixture.Db.SaveChangesAsync();

        Assert.Equal("Root Club - Open match", (await fixture.FindAsync("2026-10-22", "18:00")).Match!.Description);
        Assert.Equal("Root Club - Closed match", (await fixture.FindAsync("2026-10-22", "20:00")).Match!.Description);
    }

    [Fact]
    public async Task More_than_one_match_is_ambiguous_and_returns_no_results()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.AddMatch(fixture.RootTenantId, "Lane A", "19:30");
        fixture.AddMatch(fixture.ChildTenantId, "Lane B", "19:30");
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.FindAsync("2026-10-22", "19:30");

        Assert.Equal(MatchFindOutcome.Ambiguous, result.Outcome);
        Assert.Null(result.Match);
        Assert.Equal("MATCH_AMBIGUOUS", result.Error!.Code);
        Assert.Contains("Root Club - Lane A", result.Error.Message);
        Assert.Contains("Child Club - Lane B", result.Error.Message);
    }

    [Fact]
    public async Task Two_matches_in_the_same_tenant_are_ambiguous_too()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.AddMatch(fixture.RootTenantId, "Lane A", "19:30");
        fixture.AddMatch(fixture.RootTenantId, "Lane B", "19:30");
        await fixture.Db.SaveChangesAsync();

        Assert.Equal(MatchFindOutcome.Ambiguous, (await fixture.FindAsync("2026-10-22", "19:30")).Outcome);
    }

    [Theory]
    [InlineData(null, "19:30", "INVALID_DATE")]
    [InlineData("22-10-2026", "19:30", "INVALID_DATE")]
    [InlineData("2026-02-30", "19:30", "INVALID_DATE")]
    [InlineData("2026-10-22", null, "INVALID_STARTTIME")]
    [InlineData("2026-10-22", "25:00", "INVALID_STARTTIME")]
    [InlineData("2026-10-22", "7pm", "INVALID_STARTTIME")]
    public async Task Invalid_input_is_rejected_with_a_coded_error(string? date, string? startTime, string expectedCode)
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.FindAsync(date, startTime);

        Assert.Equal(MatchFindOutcome.InvalidInput, result.Outcome);
        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Fact]
    public async Task Reserved_and_duplicate_category_property_names_are_skipped()
    {
        await using var fixture = await Fixture.CreateAsync();
        // A category literally named "Score" (no API name) must not overwrite the score.
        var scoreId = fixture.AddCategory("Score", null, (1, "High"));
        // "A klasse" and "B klasse" both resolve to "klasse": the first by category name wins.
        var firstId = fixture.AddCategory("A klasse", "klasse", (1, "First"));
        var secondId = fixture.AddCategory("B klasse", "Klasse", (1, "Second"));
        var match = fixture.AddMatch(fixture.RootTenantId, "Evening", "19:30");
        fixture.AddParticipant(match, "Amy Archer", "1", new Dictionary<Guid, int> { [scoreId] = 1, [firstId] = 1, [secondId] = 1 }, null, 7);
        await fixture.Db.SaveChangesAsync();

        var row = Assert.Single((await fixture.FindAsync("2026-10-22", "19:30")).Match!.Results);

        Assert.Equal(7, row["score"]);
        Assert.Equal("First", row["klasse"]);
        Assert.Equal(["name", "score", "lidnummer", "klasse"], row.Keys);
    }

    [Fact]
    public async Task Categories_of_another_tenant_are_not_used()
    {
        await using var fixture = await Fixture.CreateAsync();
        var foreignId = Guid.NewGuid();
        fixture.Db.Categories.Add(new Category { Id = foreignId, TenantId = fixture.UnrelatedTenantId, Name = "Foreign", Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = fixture.UnrelatedTenantId, CategoryId = foreignId, ValueId = 1, Name = "X" }] });
        var match = fixture.AddMatch(fixture.RootTenantId, "Evening", "19:30");
        fixture.AddParticipant(match, "Amy Archer", "1", new Dictionary<Guid, int> { [foreignId] = 1 }, null);
        await fixture.Db.SaveChangesAsync();

        var row = Assert.Single((await fixture.FindAsync("2026-10-22", "19:30")).Match!.Results);

        Assert.False(row.ContainsKey("Foreign"));
    }

    [Fact]
    public void Extra_request_properties_are_not_an_error_and_are_not_mapped()
    {
        const string body = """{ "date": "2026-10-22", "starttime": "19:30", "verenigingsnummer": 1231, "afstand": 18, "aantal pijlen": 30 }""";

        var request = JsonSerializer.Deserialize<FindMatchRequest>(body, JsonSerializerOptions.Web);

        Assert.Equal(new FindMatchRequest("2026-10-22", "19:30"), request);
    }

    [Fact]
    public async Task Controller_maps_outcomes_to_status_codes_with_a_token_that_has_no_tenant_selected()
    {
        await using var fixture = await Fixture.CreateAsync();
        var match = fixture.AddMatch(fixture.RootTenantId, "Evening", "19:30");
        fixture.AddParticipant(match, "Amy Archer", "1", [], null, 10);
        fixture.AddMatch(fixture.RootTenantId, "Lane A", "20:00");
        fixture.AddMatch(fixture.RootTenantId, "Lane B", "20:00");
        await fixture.Db.SaveChangesAsync();
        var controller = new MatchFindController(fixture.Service, new LoginTokenContext(fixture.AccountId));

        var ok = Assert.IsType<OkObjectResult>(await controller.Find(new FindMatchRequest("2026-10-22", "19:30"), CancellationToken.None));
        Assert.Equal("Root Club - Evening", Assert.IsType<FoundMatch>(ok.Value).Description);
        Assert.IsType<NotFoundObjectResult>(await controller.Find(new FindMatchRequest("2026-10-22", "07:00"), CancellationToken.None));
        Assert.IsType<ConflictObjectResult>(await controller.Find(new FindMatchRequest("2026-10-22", "20:00"), CancellationToken.None));
        Assert.IsType<BadRequestObjectResult>(await controller.Find(new FindMatchRequest("bad", "19:30"), CancellationToken.None));
    }

    [Fact]
    public void Controller_requires_authentication_but_bypasses_the_tenant_selection_guard()
    {
        var type = typeof(MatchFindController);

        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(type.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(type.GetMethod(nameof(MatchFindController.Find))!.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.False(type.IsSubclassOf(typeof(ApiControllerBase)));
        Assert.Equal("api/matches", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("find", type.GetMethod(nameof(MatchFindController.Find))!.GetCustomAttribute<HttpPostAttribute>()!.Template);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public ApplicationDbContext Db { get; }
        public MatchFindService Service { get; }
        public Guid RootTenantId { get; } = Guid.NewGuid();
        public Guid ChildTenantId { get; } = Guid.NewGuid();
        public Guid UnrelatedTenantId { get; } = Guid.NewGuid();
        public Guid AccountId { get; } = Guid.NewGuid();

        private Fixture(SqliteConnection connection, ApplicationDbContext db)
        {
            this.connection = connection;
            Db = db;
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Secret"] = "test-secret-that-is-long-enough-for-jwt" }).Build();
            Service = new MatchFindService(db, new AuthService(db, configuration), new ScoringService());
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var fixture = new Fixture(connection, db);
            db.Tenants.AddRange(
                new Tenant { Id = fixture.RootTenantId, Name = "Root Club" },
                new Tenant { Id = fixture.ChildTenantId, Name = "Child Club", ParentTenantId = fixture.RootTenantId },
                new Tenant { Id = fixture.UnrelatedTenantId, Name = "Other Club" });
            db.Accounts.Add(new Account { Id = fixture.AccountId, TenantId = fixture.RootTenantId, Username = "results", PasswordHash = Passwords.Hash("secret"), Authorization = AuthorizationProfile.Viewer });
            await db.SaveChangesAsync();
            return fixture;
        }

        public Task<MatchFindResult> FindAsync(string? date, string? startTime) => Service.FindAsync(AccountId, new FindMatchRequest(date, startTime), CancellationToken.None);

        public Guid AddCategory(string name, string? apiName, params (int ValueId, string Name)[] values)
        {
            var categoryId = Guid.NewGuid();
            Db.Categories.Add(new Category
            {
                Id = categoryId,
                TenantId = RootTenantId,
                Name = name,
                ApiName = apiName,
                Values = values.Select(value => new CategoryValue { Id = Guid.NewGuid(), TenantId = RootTenantId, CategoryId = categoryId, ValueId = value.ValueId, Name = value.Name }).ToList()
            });
            return categoryId;
        }

        public Match AddMatch(Guid tenantId, string name, string? shortCode, bool isOpen = false)
        {
            var match = new Match { Id = Guid.NewGuid(), TenantId = tenantId, Name = name, Date = MatchDate, ShortCode = shortCode, IsOpen = isOpen, Ends = 10, ArrowsPerEnd = 3 };
            Db.Matches.Add(match);
            return match;
        }

        public void AddParticipant(Match match, string fullName, string? federationNumber, Dictionary<Guid, int> categories, Guid? memberId, params int[] arrowValues)
        {
            var participantId = Guid.NewGuid();
            match.Participants.Add(new MatchParticipant
            {
                Id = participantId,
                TenantId = match.TenantId,
                MatchId = match.Id,
                ParticipantListMemberId = memberId,
                OwnFullName = fullName,
                OwnFederationNumber = federationNumber,
                OwnCategories = categories,
                Scores = arrowValues.Select((value, index) => new ArrowScore { Id = Guid.NewGuid(), TenantId = match.TenantId, MatchParticipantId = participantId, End = 1, Arrow = index + 1, KeyId = value.ToString(), Value = value }).ToList()
            });
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    // Mirrors the claims of a token straight from POST api/auth/login: no tenant selected yet.
    private sealed class LoginTokenContext(Guid accountId) : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public Guid AccountId { get; } = accountId;
        public bool IsAdministrator => false;
        public bool CanManage => false;
        public DateTime TokenExpiresAtUtc => DateTime.UtcNow.AddHours(4);
    }
}
