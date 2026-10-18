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
            .Include(item => item.Participants).ThenInclude(item => item.ParticipantListMember)
            .Include(item => item.Devices)
            .Include(item => item.LiveScopes)
            .Where(item => tenantIds.Contains(item.TenantId))
            .ToListAsync(cancellationToken);
        // Separate query on purpose: signature images are large, and joining them into the match graph
        // above is what once blew up MariaDB's temp tables (see MatchParticipantSignature).
        var signatures = await db.MatchParticipantSignatures.AsNoTracking()
            .Where(item => tenantIds.Contains(item.TenantId))
            .ToDictionaryAsync(item => item.MatchParticipantId, cancellationToken);

        return matches.Select(match => (match.Id, (object)new MatchBackup(
            match.Id, match.TenantId, match.Name, match.Date, match.ShortCode, match.IsOpen, match.ParticipantListId,
            match.DeviceSelectionMode, match.Ends, match.ArrowsPerEnd, match.GroupEnds, match.AllowFreeParticipants,
            match.KeyboardJson, match.ScoringRulesJson, match.PersonalBestClassifier,
            match.Participants.Select(participant => new MatchParticipantBackup(
                participant.Id, participant.ParticipantListMemberId, participant.LastName, participant.FullName, participant.FederationNumber,
                participant.Categories, participant.DeviceId, participant.DeviceOrder, participant.DeviceLane,
                participant.Scores.Select(score => new ArrowScoreBackup(score.End, score.Arrow, score.KeyId, score.Value)).ToList(),
                participant.Signed, participant.SignedAtUtc,
                signatures.GetValueOrDefault(participant.Id)?.ArcherSignatureDataUrl, signatures.GetValueOrDefault(participant.Id)?.MarkerSignatureDataUrl)).ToList(),
            match.Devices.Select(device => new ScoreDeviceBackup(device.Id, device.Name, device.SortOrder)).ToList(),
            match.LiveScopes.Select(scope => new LiveScoreScopeBackup(scope.Id, scope.Scope, scope.GroupByCategoryIdsJson, scope.IncludeAverage, scope.IncludeGroupScores, scope.IncludeEqualizers, scope.IncludePersonalBest)).ToList(),
            match.SignatureMode))).ToList();
    }

    public async Task<BackupImportOutcome> ImportAsync(BackupImportContext context, CancellationToken cancellationToken)
    {
        var entries = context.ReadEntries<MatchBackup>(FolderName);
        var warnings = new List<string>();
        var matches = new List<Match>();
        var signatures = new List<MatchParticipantSignature>();

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
                    // Own* is populated from the backup snapshot regardless of link status, as a fallback:
                    // if the remap above fails (the roster member wasn't part of this backup), the
                    // restored participant still shows the name/number/categories it had when backed up.
                    ParticipantListMemberId = participant.ParticipantListMemberId is { } memberId && context.TryRemap(memberId, out var newMemberId) ? newMemberId : null,
                    OwnLastName = participant.LastName,
                    OwnFullName = participant.FullName,
                    OwnFederationNumber = participant.FederationNumber,
                    OwnCategories = BackupRemapHelpers.RemapCategoryDictionary(participant.Categories, context),
                    DeviceId = participant.DeviceId is { } deviceId && context.TryRemap(deviceId, out var newDeviceId) ? newDeviceId : null,
                    DeviceOrder = participant.DeviceOrder,
                    DeviceLane = participant.DeviceLane,
                    Scores = participant.Scores.Select(score => new ArrowScore { Id = Guid.NewGuid(), TenantId = newTenantId, MatchParticipantId = newParticipantId, End = score.End, Arrow = score.Arrow, KeyId = score.KeyId, Value = score.Value }).ToList(),
                    Signed = participant.Signed,
                    SignedAtUtc = participant.SignedAtUtc
                };
            }).ToList();

            signatures.AddRange(entry.Participants
                .Where(participant => (participant.ArcherSignatureDataUrl is not null || participant.MarkerSignatureDataUrl is not null) && context.IdMap.ContainsKey(participant.Id))
                .Select(participant => new MatchParticipantSignature { MatchParticipantId = context.IdMap[participant.Id], TenantId = newTenantId, ArcherSignatureDataUrl = participant.ArcherSignatureDataUrl, MarkerSignatureDataUrl = participant.MarkerSignatureDataUrl }));

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
                KeyboardJson = BackupRemapHelpers.RemapKeyboardConfigJson(entry.KeyboardJson, context),
                ScoringRulesJson = entry.ScoringRulesJson,
                PersonalBestClassifier = entry.PersonalBestClassifier,
                SignatureMode = entry.SignatureMode,
                Participants = participants,
                Devices = devices,
                LiveScopes = liveScopes
            });
        }

        context.Db.Matches.AddRange(matches);
        context.Db.MatchParticipantSignatures.AddRange(signatures);
        await context.Db.SaveChangesAsync(cancellationToken);
        return new BackupImportOutcome(matches.Count, warnings);
    }
}
