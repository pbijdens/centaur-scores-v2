using System.Text.Json;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

public sealed class MatchBackupHandler : IBackupHandler
{
    public string FolderName => "matches";

    public async Task<IReadOnlyList<(Guid, object)>> ExportAsync(ApplicationDbContext db, IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var matches = await db.Matches.AsNoTracking()
            .Include(item => item.Participants).ThenInclude(item => item.Scores)
            .Include(item => item.Devices)
            .Include(item => item.LiveScopes)
            .Where(item => tenantIds.Contains(item.TenantId))
            .ToListAsync(cancellationToken);

        return matches.Select(match => (match.Id, (object)new MatchBackup(
            match.Id, match.TenantId, match.Name, match.Date, match.ShortCode, match.IsOpen, match.ParticipantListId,
            match.DeviceSelectionMode, match.Ends, match.ArrowsPerEnd, match.GroupEnds, match.AllowFreeParticipants,
            match.KeyboardJson, match.ScoringRulesJson, match.PersonalBestClassifier,
            match.Participants.Select(participant => new MatchParticipantBackup(
                participant.Id, participant.ParticipantListMemberId, participant.LastName, participant.FullName, participant.FederationNumber,
                participant.Categories, participant.DeviceId, participant.DeviceOrder,
                participant.Scores.Select(score => new ArrowScoreBackup(score.End, score.Arrow, score.KeyId, score.Value)).ToList())).ToList(),
            match.Devices.Select(device => new ScoreDeviceBackup(device.Id, device.Name, device.SortOrder)).ToList(),
            match.LiveScopes.Select(scope => new LiveScoreScopeBackup(scope.Id, scope.Scope, scope.GroupByCategoryIdsJson, scope.IncludeAverage, scope.IncludeGroupScores, scope.IncludeEqualizers, scope.IncludePersonalBest)).ToList()))).ToList();
    }

    public async Task<BackupImportOutcome> ImportAsync(BackupImportContext context, CancellationToken cancellationToken)
    {
        var entries = context.ReadEntries<MatchBackup>(FolderName);
        var warnings = new List<string>();
        var matches = new List<Match>();

        foreach (var entry in entries)
        {
            if (!context.TryRemap(entry.TenantId, out var newTenantId))
            {
                warnings.Add($"Skipped match '{entry.Name}': its owning tenant was not part of this backup.");
                continue;
            }

            var newMatchId = context.Mint(entry.Id);
            var participantListId = entry.ParticipantListId is { } listId && context.TryRemap(listId, out var newListId) ? newListId : (Guid?)null;

            // Devices must be minted before participants, since a participant's DeviceId references one of them.
            var devices = entry.Devices.Select(device => new ScoreDevice { Id = context.Mint(device.Id), TenantId = newTenantId, MatchId = newMatchId, Name = device.Name, SortOrder = device.SortOrder }).ToList();

            var participants = entry.Participants.Select(participant =>
            {
                var newParticipantId = context.Mint(participant.Id);
                return new MatchParticipant
                {
                    Id = newParticipantId,
                    TenantId = newTenantId,
                    MatchId = newMatchId,
                    ParticipantListMemberId = participant.ParticipantListMemberId is { } memberId && context.TryRemap(memberId, out var newMemberId) ? newMemberId : null,
                    LastName = participant.LastName,
                    FullName = participant.FullName,
                    FederationNumber = participant.FederationNumber,
                    Categories = BackupRemapHelpers.RemapCategoryDictionary(participant.Categories, context),
                    DeviceId = participant.DeviceId is { } deviceId && context.TryRemap(deviceId, out var newDeviceId) ? newDeviceId : null,
                    DeviceOrder = participant.DeviceOrder,
                    Scores = participant.Scores.Select(score => new ArrowScore { Id = Guid.NewGuid(), TenantId = newTenantId, MatchParticipantId = newParticipantId, End = score.End, Arrow = score.Arrow, KeyId = score.KeyId, Value = score.Value }).ToList()
                };
            }).ToList();

            var liveScopes = entry.LiveScopes.Select(scope => new LiveScoreScope
            {
                Id = context.Mint(scope.Id),
                TenantId = newTenantId,
                MatchId = newMatchId,
                Scope = scope.Scope,
                GroupByCategoryIdsJson = BackupRemapHelpers.RemapGuidJsonArray(scope.GroupByCategoryIdsJson, context),
                IncludeAverage = scope.IncludeAverage,
                IncludeGroupScores = scope.IncludeGroupScores,
                IncludeEqualizers = scope.IncludeEqualizers,
                IncludePersonalBest = scope.IncludePersonalBest
            }).ToList();

            matches.Add(new Match
            {
                Id = newMatchId,
                TenantId = newTenantId,
                Name = entry.Name,
                Date = entry.Date,
                ShortCode = entry.ShortCode,
                IsOpen = entry.IsOpen,
                ParticipantListId = participantListId,
                DeviceSelectionMode = entry.DeviceSelectionMode,
                Ends = entry.Ends,
                ArrowsPerEnd = entry.ArrowsPerEnd,
                GroupEnds = entry.GroupEnds,
                AllowFreeParticipants = entry.AllowFreeParticipants,
                KeyboardJson = entry.KeyboardJson,
                ScoringRulesJson = entry.ScoringRulesJson,
                PersonalBestClassifier = entry.PersonalBestClassifier,
                Participants = participants,
                Devices = devices,
                LiveScopes = liveScopes
            });
        }

        context.Db.Matches.AddRange(matches);
        await context.Db.SaveChangesAsync(cancellationToken);
        return new BackupImportOutcome(matches.Count, warnings);
    }
}
