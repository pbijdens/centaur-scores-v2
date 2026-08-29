using System.Text.Json;
using CentaurScores.Api.Application;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Tests;

public sealed class BackupRestoreServiceTests
{
    [Fact]
    public async Task Export_then_restore_remaps_every_reference_and_never_reuses_an_original_id()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var sourceTenantId = Guid.NewGuid();
        var adminTenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        db.Tenants.AddRange(
            new Tenant { Id = sourceTenantId, Name = "Archery Club" },
            new Tenant { Id = adminTenantId, Name = "HQ" });

        db.Accounts.Add(new Account { Id = Guid.NewGuid(), TenantId = sourceTenantId, Username = "coach", PasswordHash = Passwords.Hash("secret"), Authorization = AuthorizationProfile.Manager });

        db.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = sourceTenantId,
            Name = "Bow type",
            Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = sourceTenantId, CategoryId = categoryId, ValueId = 1, Name = "Recurve" }]
        });

        db.ParticipantLists.Add(new ParticipantList
        {
            Id = listId,
            TenantId = sourceTenantId,
            Name = "Members",
            Members = [new ParticipantListMember { Id = memberId, TenantId = sourceTenantId, ParticipantListId = listId, LastName = "Archer", FullName = "Robin Archer", Categories = new Dictionary<Guid, int> { [categoryId] = 1 } }]
        });

        db.Matches.Add(new Match
        {
            Id = matchId,
            TenantId = sourceTenantId,
            Name = "Open",
            ParticipantListId = listId,
            Devices = [new ScoreDevice { Id = deviceId, TenantId = sourceTenantId, MatchId = matchId, Name = "Target 1" }],
            Participants =
            [
                new MatchParticipant
                {
                    Id = participantId,
                    TenantId = sourceTenantId,
                    MatchId = matchId,
                    ParticipantListMemberId = memberId,
                    FullName = "Robin Archer",
                    LastName = "Archer",
                    Categories = new Dictionary<Guid, int> { [categoryId] = 1 },
                    DeviceId = deviceId,
                    Scores = [new ArrowScore { Id = Guid.NewGuid(), TenantId = sourceTenantId, MatchParticipantId = participantId, End = 1, Arrow = 1, KeyId = "X", Value = 10 }]
                }
            ]
        });

        db.Competitions.Add(new Competition
        {
            Id = competitionId,
            TenantId = sourceTenantId,
            Name = "Club Series",
            Rounds = [new CompetitionRound { Id = roundId, TenantId = sourceTenantId, CompetitionId = competitionId, ShortName = "R1", LongName = "Round 1", Matches = [new CompetitionRoundMatch { Id = Guid.NewGuid(), TenantId = sourceTenantId, CompetitionRoundId = roundId, MatchId = matchId }] }],
            ScoringRules = [new CompetitionScoreRule { Id = Guid.NewGuid(), TenantId = sourceTenantId, CompetitionId = competitionId, Name = "Total", RoundIdsJson = JsonSerializer.Serialize(new[] { roundId }) }]
        });

        db.PersonalBestDisciplines.Add(new PersonalBestDiscipline
        {
            Id = Guid.NewGuid(),
            TenantId = sourceTenantId,
            Name = "18m Recurve",
            Mappings = [new PersonalBestDisciplineMapping { Id = Guid.NewGuid(), TenantId = sourceTenantId, SourceTenantId = sourceTenantId, CategoryId = categoryId, ValueId = 1 }]
        });

        await db.SaveChangesAsync();

        var backupService = new BackupService(db);
        var (zipBytes, _) = await backupService.CreateBackupAsync(sourceTenantId, includeSubTenants: false, CancellationToken.None);

        var restoreService = new RestoreService(db);
        using var zipStream = new MemoryStream(zipBytes);
        var result = await restoreService.RestoreAsync(adminTenantId, zipStream, CancellationToken.None);

        Assert.Empty(result.Warnings);
        Assert.NotEqual(sourceTenantId, result.NewTenantId);
        Assert.Contains("restored", result.NewTenantName);

        var newTenant = await db.Tenants.SingleAsync(item => item.Id == result.NewTenantId);
        Assert.Equal(adminTenantId, newTenant.ParentTenantId);

        var newAccount = await db.Accounts.SingleAsync(item => item.TenantId == result.NewTenantId);
        Assert.EndsWith("coach", newAccount.Username);
        Assert.NotEqual("coach", newAccount.Username);
        Assert.True(Passwords.Verify("secret", newAccount.PasswordHash)); // password hash carries over verbatim (confirmed decision), so the original password still works.

        var newCategory = await db.Categories.Include(item => item.Values).SingleAsync(item => item.TenantId == result.NewTenantId);
        Assert.NotEqual(categoryId, newCategory.Id);
        var newValueId = Assert.Single(newCategory.Values).ValueId;
        Assert.Equal(1, newValueId);

        var newMember = await db.ParticipantListMembers.SingleAsync(item => item.TenantId == result.NewTenantId);
        Assert.NotEqual(memberId, newMember.Id);
        Assert.Equal(newCategory.Id, Assert.Single(newMember.Categories).Key);

        var newMatch = await db.Matches.Include(item => item.Participants).Include(item => item.Devices).SingleAsync(item => item.TenantId == result.NewTenantId);
        Assert.NotEqual(matchId, newMatch.Id);
        Assert.Equal(newMember.ParticipantListId, newMatch.ParticipantListId);
        var newDevice = Assert.Single(newMatch.Devices);
        Assert.NotEqual(deviceId, newDevice.Id);
        var newParticipant = Assert.Single(newMatch.Participants);
        Assert.Equal(newMember.Id, newParticipant.ParticipantListMemberId);
        Assert.Equal(newDevice.Id, newParticipant.DeviceId);
        Assert.Equal(newCategory.Id, Assert.Single(newParticipant.Categories).Key);

        var newScore = await db.ArrowScores.SingleAsync(item => item.TenantId == result.NewTenantId);
        Assert.Equal(newParticipant.Id, newScore.MatchParticipantId);
        Assert.Equal(10, newScore.Value);

        var newCompetition = await db.Competitions.Include(item => item.Rounds).ThenInclude(item => item.Matches).Include(item => item.ScoringRules).SingleAsync(item => item.TenantId == result.NewTenantId);
        var newRound = Assert.Single(newCompetition.Rounds);
        Assert.NotEqual(roundId, newRound.Id);
        var newRoundMatch = Assert.Single(newRound.Matches);
        Assert.Equal(newMatch.Id, newRoundMatch.MatchId);
        var newRule = Assert.Single(newCompetition.ScoringRules);
        var ruleRoundIds = JsonSerializer.Deserialize<List<Guid>>(newRule.RoundIdsJson)!;
        Assert.Equal(newRound.Id, Assert.Single(ruleRoundIds));

        var newMapping = await db.PersonalBestDisciplineMappings.SingleAsync(item => item.TenantId == result.NewTenantId);
        Assert.Equal(result.NewTenantId, newMapping.SourceTenantId);
        Assert.Equal(newCategory.Id, newMapping.CategoryId);
        Assert.Equal(1, newMapping.ValueId);
    }

    [Fact]
    public async Task Discipline_mapping_pointing_outside_the_exported_tenant_set_is_dropped_with_a_warning()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var parentTenantId = Guid.NewGuid();
        var childTenantId = Guid.NewGuid();
        var adminTenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        db.Tenants.AddRange(
            new Tenant { Id = parentTenantId, Name = "Parent" },
            new Tenant { Id = childTenantId, Name = "Child", ParentTenantId = parentTenantId },
            new Tenant { Id = adminTenantId, Name = "HQ" });
        db.Categories.Add(new Category { Id = categoryId, TenantId = childTenantId, Name = "Bow type", Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = childTenantId, CategoryId = categoryId, ValueId = 1, Name = "Recurve" }] });
        db.PersonalBestDisciplines.Add(new PersonalBestDiscipline
        {
            Id = Guid.NewGuid(),
            TenantId = parentTenantId,
            Name = "18m Recurve",
            Mappings = [new PersonalBestDisciplineMapping { Id = Guid.NewGuid(), TenantId = parentTenantId, SourceTenantId = childTenantId, CategoryId = categoryId, ValueId = 1 }]
        });
        await db.SaveChangesAsync();

        var backupService = new BackupService(db);
        var (zipBytes, _) = await backupService.CreateBackupAsync(parentTenantId, includeSubTenants: false, CancellationToken.None);

        var restoreService = new RestoreService(db);
        using var zipStream = new MemoryStream(zipBytes);
        var result = await restoreService.RestoreAsync(adminTenantId, zipStream, CancellationToken.None);

        var newDiscipline = await db.PersonalBestDisciplines.Include(item => item.Mappings).SingleAsync(item => item.TenantId == result.NewTenantId);
        Assert.Equal("18m Recurve", newDiscipline.Name);
        Assert.Empty(newDiscipline.Mappings);
        Assert.Contains(result.Warnings, warning => warning.Contains("excluded"));
    }
}
