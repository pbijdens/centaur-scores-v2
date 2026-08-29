using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

public sealed class CompetitionBackupHandler : IBackupHandler
{
    public string FolderName => "competitions";

    public async Task<IReadOnlyList<(Guid, object)>> ExportAsync(ApplicationDbContext db, IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var competitions = await db.Competitions.AsNoTracking()
            .Include(item => item.Rounds).ThenInclude(item => item.Matches)
            .Include(item => item.ScoringRules)
            .Where(item => tenantIds.Contains(item.TenantId))
            .ToListAsync(cancellationToken);

        return competitions.Select(competition => (competition.Id, (object)new CompetitionBackup(
            competition.Id, competition.TenantId, competition.Name, competition.StartDate, competition.EndDate, competition.GroupByCategoryIdsJson,
            competition.Rounds.Select(round => new CompetitionRoundBackup(
                round.Id, round.Order, round.ShortName, round.LongName,
                round.Matches.Select(match => new CompetitionRoundMatchBackup(match.MatchId)).ToList())).ToList(),
            competition.ScoringRules.Select(rule => new CompetitionScoreRuleBackup(rule.Name, rule.RoundIdsJson, rule.HighestScores, rule.MinimumScores, rule.Aggregation, rule.SortOrder)).ToList()))).ToList();
    }

    public async Task<BackupImportOutcome> ImportAsync(BackupImportContext context, CancellationToken cancellationToken)
    {
        var entries = context.ReadEntries<CompetitionBackup>(FolderName);
        var warnings = new List<string>();
        var competitions = new List<Competition>();

        foreach (var entry in entries)
        {
            if (!context.TryRemap(entry.TenantId, out var newTenantId))
            {
                warnings.Add($"Skipped competition '{entry.Name}': its owning tenant was not part of this backup.");
                continue;
            }

            var newCompetitionId = context.Mint(entry.Id);

            var rounds = entry.Rounds.Select(round =>
            {
                var newRoundId = context.Mint(round.Id);
                var matches = round.Matches
                    .Where(match => context.TryRemap(match.MatchId, out _))
                    .Select(match => new CompetitionRoundMatch { Id = Guid.NewGuid(), TenantId = newTenantId, CompetitionRoundId = newRoundId, MatchId = context.IdMap[match.MatchId] })
                    .ToList();
                if (matches.Count != round.Matches.Count)
                {
                    warnings.Add($"Competition '{entry.Name}', round '{round.ShortName}': {round.Matches.Count - matches.Count} match link(s) dropped (the match was not part of this backup).");
                }
                return new CompetitionRound { Id = newRoundId, TenantId = newTenantId, CompetitionId = newCompetitionId, Order = round.Order, ShortName = round.ShortName, LongName = round.LongName, Matches = matches };
            }).ToList();

            var scoringRules = entry.ScoringRules.Select(rule => new CompetitionScoreRule
            {
                Id = Guid.NewGuid(),
                TenantId = newTenantId,
                CompetitionId = newCompetitionId,
                Name = rule.Name,
                RoundIdsJson = BackupRemapHelpers.RemapGuidJsonArray(rule.RoundIdsJson, context),
                HighestScores = rule.HighestScores,
                MinimumScores = rule.MinimumScores,
                Aggregation = rule.Aggregation,
                SortOrder = rule.SortOrder
            }).ToList();

            competitions.Add(new Competition
            {
                Id = newCompetitionId,
                TenantId = newTenantId,
                Name = entry.Name,
                StartDate = entry.StartDate,
                EndDate = entry.EndDate,
                GroupByCategoryIdsJson = BackupRemapHelpers.RemapGuidJsonArray(entry.GroupByCategoryIdsJson, context),
                Rounds = rounds,
                ScoringRules = scoringRules
            });
        }

        context.Db.Competitions.AddRange(competitions);
        await context.Db.SaveChangesAsync(cancellationToken);
        return new BackupImportOutcome(competitions.Count, warnings);
    }
}
