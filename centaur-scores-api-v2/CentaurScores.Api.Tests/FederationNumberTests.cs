using Regex = System.Text.RegularExpressions.Regex;
using GeneratedRegexAttribute = System.Text.RegularExpressions.GeneratedRegexAttribute;
using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Controllers;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CentaurScores.Api.Tests;

// Temporary "CS" federation numbers, the personal-best preview/renumber on a member's number change, and
// personal-best reprocessing when a closed match's participant is linked to a different list member.
public sealed partial class FederationNumberTests
{
    [GeneratedRegex("^CS[0-9]{10}$")]
    private static partial Regex TemporaryNumberPattern();

    private static async Task<ApplicationDbContext> NewDbAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static ParticipantListsController NewListsController(ApplicationDbContext db, Guid tenantId) =>
        new(db, new TestTenantContext(tenantId), new ParticipantListExcelService(), new FederationNumberService(db), new PersonalBestNumberChanges(db, new PersonalBestContext(db)));

    private static MatchesController NewMatchesController(ApplicationDbContext db, Guid tenantId)
    {
        var scoring = new ScoringService();
        var personalBestContext = new PersonalBestContext(db);
        var personalBestEngine = new PersonalBestEngine(db);
        return new MatchesController(db, new TestTenantContext(tenantId), scoring, new LiveScoringService(scoring), new PersonalBestRegistrationService(db, personalBestContext, personalBestEngine), new PersonalBestLiveLookup(db, personalBestContext, personalBestEngine, new MemoryCache(new MemoryCacheOptions())));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("0", true)]
    [InlineData(" 000 ", true)]
    [InlineData("10", false)]
    [InlineData("CS0000000001", false)]
    public void IsMissing_treats_empty_blank_and_zero_numbers_as_missing(string? value, bool expected) =>
        Assert.Equal(expected, FederationNumberService.IsMissing(value));

    [Fact]
    public async Task GenerateAsync_returns_distinct_temporary_numbers()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var service = new FederationNumberService(db);

        var numbers = new List<string>();
        for (var index = 0; index < 20; index++) numbers.Add(await service.GenerateAsync(CancellationToken.None));

        Assert.All(numbers, number => Assert.Matches(TemporaryNumberPattern(), number));
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
        Assert.Equal("1234", await service.EnsureAsync("1234", CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("0")]
    public async Task AddMember_assigns_a_temporary_number_when_the_number_is_missing(string? federationNumber)
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var tenantId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        db.AddRange(new Tenant { Id = tenantId, Name = "Club" }, new ParticipantList { Id = listId, TenantId = tenantId, Name = "List" });
        await db.SaveChangesAsync();

        var result = Assert.IsType<CreatedResult>(await NewListsController(db, tenantId).AddMember(listId, new CreateParticipantRequest("Archer", "Robin Archer", federationNumber, []), CancellationToken.None));

        var member = Assert.IsType<ParticipantListMember>(result.Value);
        Assert.Matches(TemporaryNumberPattern(), member.FederationNumber);
    }

    [Fact]
    public async Task AddMember_keeps_a_real_number()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var tenantId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        db.AddRange(new Tenant { Id = tenantId, Name = "Club" }, new ParticipantList { Id = listId, TenantId = tenantId, Name = "List" });
        await db.SaveChangesAsync();

        var result = Assert.IsType<CreatedResult>(await NewListsController(db, tenantId).AddMember(listId, new CreateParticipantRequest("Archer", "Robin Archer", "123456", []), CancellationToken.None));

        Assert.Equal("123456", Assert.IsType<ParticipantListMember>(result.Value).FederationNumber);
    }

    // Free match participants are entered unmoderated (anyone on a scoring device), so they never get a
    // temporary number - only adding them to a participant list does.
    [Fact]
    public async Task AddParticipant_keeps_a_free_participants_number_exactly_as_entered()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var tenantId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        db.AddRange(new Tenant { Id = tenantId, Name = "Club" }, new Match { Id = matchId, TenantId = tenantId, Name = "Match", Ends = 1, ArrowsPerEnd = 3 });
        await db.SaveChangesAsync();

        var result = Assert.IsType<CreatedResult>(await NewMatchesController(db, tenantId).AddParticipant(matchId, new CreateMatchParticipantRequest(null, "Archer", "Robin Archer", "", []), CancellationToken.None));

        Assert.Equal("", Assert.IsType<MatchParticipant>(result.Value).OwnFederationNumber);
    }

    [Fact]
    public async Task Member_personal_bests_preview_and_opt_in_renumber_use_the_owning_super_tenant()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var parent = new Tenant { Id = Guid.NewGuid(), Name = "Federation", PersonalBestEnabled = true };
        var child = new Tenant { Id = Guid.NewGuid(), Name = "Club", ParentTenantId = parent.Id };
        var listId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        db.AddRange(parent, child, new ParticipantList { Id = listId, TenantId = child.Id, Name = "List" });
        db.ParticipantListMembers.Add(new ParticipantListMember { Id = memberId, TenantId = child.Id, ParticipantListId = listId, LastName = "Archer", FullName = "Robin Archer", FederationNumber = "111" });
        for (var day = 1; day <= 60; day++)
        {
            db.PersonalBestLogEntries.Add(new PersonalBestLogEntry { Id = Guid.NewGuid(), TenantId = parent.Id, FederationNumber = "111", Discipline = "Recurve", MatchClassifier = "Indoor", Score = 200 + day, Date = new DateOnly(2026, 1, 1).AddDays(day), RecordedAt = DateTime.UtcNow });
        }
        db.PersonalBestArcherNames.Add(new PersonalBestArcherName { Id = Guid.NewGuid(), TenantId = parent.Id, FederationNumber = "111", Name = "Robin Archer" });
        await db.SaveChangesAsync();
        var controller = NewListsController(db, child.Id);

        var preview = Assert.IsType<MemberPersonalBestEntries>(Assert.IsType<OkObjectResult>(await controller.MemberPersonalBests(listId, memberId, CancellationToken.None)).Value);
        Assert.Equal(60, preview.TotalCount);
        Assert.Equal(50, preview.Recent.Count);
        Assert.Equal(260, preview.Recent[0].Score);

        // Without the opt-in flag (plain API update) the personal-best entries are left alone.
        await controller.UpdateMember(listId, memberId, new CreateParticipantRequest("Archer", "Robin Archer", "222", []), CancellationToken.None);
        Assert.Equal(60, await db.PersonalBestLogEntries.CountAsync(item => item.FederationNumber == "111"));

        await controller.UpdateMember(listId, memberId, new CreateParticipantRequest("Archer", "Robin Archer", "333", []), CancellationToken.None);
        Assert.Equal(60, await db.PersonalBestLogEntries.CountAsync(item => item.FederationNumber == "111"));

        // Back to 111 first, then change with the opt-in: everything follows the member to the new number.
        await controller.UpdateMember(listId, memberId, new CreateParticipantRequest("Archer", "Robin Archer", "111", []), CancellationToken.None);
        await controller.UpdateMember(listId, memberId, new CreateParticipantRequest("Archer", "Robin Archer", "444", []), CancellationToken.None, updatePersonalBests: true);
        Assert.Equal(0, await db.PersonalBestLogEntries.CountAsync(item => item.FederationNumber == "111"));
        Assert.Equal(60, await db.PersonalBestLogEntries.CountAsync(item => item.FederationNumber == "444" && item.TenantId == parent.Id));
        Assert.Equal("444", (await db.PersonalBestArcherNames.SingleAsync()).FederationNumber);
    }

    // A closed match with a "Recurve"/"Outdoor" personal-best setup and one participant scoring 10.
    private sealed record ClosedMatchSetup(Guid TenantId, Guid ListId, Guid MatchId, Guid ParticipantId, Guid CategoryId);

    private static async Task<ClosedMatchSetup> SeedClosedMatchAsync(ApplicationDbContext db)
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Club", PersonalBestEnabled = true };
        var categoryId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        db.Tenants.Add(tenant);
        db.ParticipantLists.Add(new ParticipantList { Id = listId, TenantId = tenant.Id, Name = "List" });
        db.PersonalBestClassifiers.Add(new PersonalBestClassifier { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Outdoor" });
        db.PersonalBestDisciplines.Add(new PersonalBestDiscipline
        {
            Id = disciplineId,
            TenantId = tenant.Id,
            Name = "Recurve",
            Mappings = [new PersonalBestDisciplineMapping { Id = Guid.NewGuid(), TenantId = tenant.Id, DisciplineId = disciplineId, SourceTenantId = tenant.Id, CategoryId = categoryId, ValueId = 1 }]
        });
        db.Matches.Add(new Match { Id = matchId, TenantId = tenant.Id, Name = "Match", IsOpen = false, ParticipantListId = listId, Date = new DateOnly(2026, 5, 1), Ends = 1, ArrowsPerEnd = 3, PersonalBestClassifier = "Outdoor" });
        await db.SaveChangesAsync();
        db.MatchParticipants.Add(new MatchParticipant
        {
            Id = participantId,
            TenantId = tenant.Id,
            MatchId = matchId,
            OwnFullName = "Robin Archer",
            OwnLastName = "Archer",
            OwnCategories = new Dictionary<Guid, int> { [categoryId] = 1 },
            Scores = [new ArrowScore { Id = Guid.NewGuid(), TenantId = tenant.Id, MatchParticipantId = participantId, End = 1, Arrow = 1, KeyId = "10", Value = 10 }]
        });
        await db.SaveChangesAsync();
        return new(tenant.Id, listId, matchId, participantId, categoryId);
    }

    private static ParticipantListMember NewMember(ClosedMatchSetup setup, string federationNumber) =>
        new() { Id = Guid.NewGuid(), TenantId = setup.TenantId, ParticipantListId = setup.ListId, LastName = "Archer", FullName = "Robin Archer", FederationNumber = federationNumber, Categories = new Dictionary<Guid, int> { [setup.CategoryId] = 1 } };

    [Fact]
    public async Task Linking_a_free_participant_of_a_closed_match_registers_the_personal_best()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var setup = await SeedClosedMatchAsync(db);
        var member = NewMember(setup, "555");
        db.ParticipantListMembers.Add(member);
        await db.SaveChangesAsync();

        Assert.IsType<OkObjectResult>(await NewMatchesController(db, setup.TenantId).UpdateParticipant(setup.MatchId, setup.ParticipantId, new CreateMatchParticipantRequest(member.Id, "Archer", "Robin Archer", "555", []), CancellationToken.None));

        var entry = Assert.Single(db.PersonalBestLogEntries);
        Assert.Equal("555", entry.FederationNumber);
        Assert.Equal(10, entry.Score);
        Assert.Equal(new DateOnly(2026, 5, 1), entry.Date);
    }

    [Fact]
    public async Task Replacing_the_member_of_a_closed_match_moves_the_personal_best_and_restores_pruned_later_results()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var setup = await SeedClosedMatchAsync(db);
        var oldMember = NewMember(setup, "OLD");
        var newMember = NewMember(setup, "NEW");
        db.ParticipantListMembers.AddRange(oldMember, newMember);
        await db.SaveChangesAsync();
        var participant = await db.MatchParticipants.SingleAsync(item => item.Id == setup.ParticipantId);
        participant.ParticipantListMemberId = oldMember.Id;
        participant.OwnCategories = [];

        // A later closed match where the old member scored lower (9); registering the first match (10) prunes it.
        var laterMatchId = Guid.NewGuid();
        var laterParticipantId = Guid.NewGuid();
        db.Matches.Add(new Match { Id = laterMatchId, TenantId = setup.TenantId, Name = "Later", IsOpen = false, ParticipantListId = setup.ListId, Date = new DateOnly(2026, 6, 1), Ends = 1, ArrowsPerEnd = 3, PersonalBestClassifier = "Outdoor" });
        await db.SaveChangesAsync();
        db.MatchParticipants.Add(new MatchParticipant
        {
            Id = laterParticipantId,
            TenantId = setup.TenantId,
            MatchId = laterMatchId,
            ParticipantListMemberId = oldMember.Id,
            Scores = [new ArrowScore { Id = Guid.NewGuid(), TenantId = setup.TenantId, MatchParticipantId = laterParticipantId, End = 1, Arrow = 1, KeyId = "9", Value = 9 }]
        });
        await db.SaveChangesAsync();
        var registration = new PersonalBestRegistrationService(db, new PersonalBestContext(db), new PersonalBestEngine(db));
        await registration.RegisterOnDeactivationAsync(await db.Matches.AsNoTracking().SingleAsync(item => item.Id == laterMatchId), CancellationToken.None);
        await registration.RegisterOnDeactivationAsync(await db.Matches.AsNoTracking().SingleAsync(item => item.Id == setup.MatchId), CancellationToken.None);
        var registered = Assert.Single(db.PersonalBestLogEntries.AsNoTracking());
        Assert.Equal(("OLD", 10), (registered.FederationNumber, registered.Score));
        db.ChangeTracker.Clear();

        Assert.IsType<OkObjectResult>(await NewMatchesController(db, setup.TenantId).UpdateParticipant(setup.MatchId, setup.ParticipantId, new CreateMatchParticipantRequest(newMember.Id, "Archer", "Robin Archer", "NEW", []), CancellationToken.None));

        var entries = await db.PersonalBestLogEntries.AsNoTracking().OrderBy(item => item.FederationNumber).ToListAsync();
        Assert.Collection(entries,
            entry => Assert.Equal(("NEW", 10, new DateOnly(2026, 5, 1)), (entry.FederationNumber, entry.Score, entry.Date)),
            entry => Assert.Equal(("OLD", 9, new DateOnly(2026, 6, 1)), (entry.FederationNumber, entry.Score, entry.Date)));
    }

    // MatchParticipant.FederationNumber falls back to the participant's own number when the linked member has
    // none (e.g. a participant restored from a backup keeps a copy there). Registration must only ever use the
    // moderated list member's number, so a member without a number registers nothing.
    [Fact]
    public async Task Closing_a_match_never_registers_a_linked_participants_own_fallback_number()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await NewDbAsync(connection);
        var setup = await SeedClosedMatchAsync(db);
        var member = NewMember(setup, "");
        member.FederationNumber = null;
        db.ParticipantListMembers.Add(member);
        await db.SaveChangesAsync();
        var participant = await db.MatchParticipants.SingleAsync(item => item.Id == setup.ParticipantId);
        participant.ParticipantListMemberId = member.Id;
        participant.OwnFederationNumber = "999";
        await db.SaveChangesAsync();

        var registration = new PersonalBestRegistrationService(db, new PersonalBestContext(db), new PersonalBestEngine(db));
        await registration.RegisterOnDeactivationAsync(await db.Matches.AsNoTracking().SingleAsync(item => item.Id == setup.MatchId), CancellationToken.None);

        Assert.Empty(db.PersonalBestLogEntries);
        Assert.Empty(db.PersonalBestArcherNames);
    }

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid AccountId { get; } = Guid.NewGuid();
        public bool IsAdministrator => true;
        public bool CanManage => true;
        public DateTime TokenExpiresAtUtc => DateTime.UtcNow.AddHours(4);
    }
}
