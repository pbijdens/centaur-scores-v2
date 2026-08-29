using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

public sealed class MatchTemplateBackupHandler : IBackupHandler
{
    public string FolderName => "templates";

    public async Task<IReadOnlyList<(Guid, object)>> ExportAsync(ApplicationDbContext db, IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var templates = await db.MatchTemplates.AsNoTracking().Where(item => tenantIds.Contains(item.TenantId)).ToListAsync(cancellationToken);
        return templates.Select(item => (item.Id, (object)new MatchTemplateBackup(item.Id, item.TenantId, item.Name, item.ParticipantListId, item.AllowFreeParticipants, item.DeviceSelectionMode, item.ConfigurationJson, item.PersonalBestClassifier))).ToList();
    }

    public async Task<BackupImportOutcome> ImportAsync(BackupImportContext context, CancellationToken cancellationToken)
    {
        var entries = context.ReadEntries<MatchTemplateBackup>(FolderName);
        var warnings = new List<string>();
        var templates = new List<MatchTemplate>();

        foreach (var entry in entries)
        {
            if (!context.TryRemap(entry.TenantId, out var newTenantId))
            {
                warnings.Add($"Skipped template '{entry.Name}': its owning tenant was not part of this backup.");
                continue;
            }

            var participantListId = entry.ParticipantListId is { } listId && context.TryRemap(listId, out var newListId) ? newListId : (Guid?)null;
            templates.Add(new MatchTemplate
            {
                Id = context.Mint(entry.Id),
                TenantId = newTenantId,
                Name = entry.Name,
                ParticipantListId = participantListId,
                AllowFreeParticipants = entry.AllowFreeParticipants,
                DeviceSelectionMode = entry.DeviceSelectionMode,
                ConfigurationJson = entry.ConfigurationJson,
                PersonalBestClassifier = entry.PersonalBestClassifier
            });
        }

        context.Db.MatchTemplates.AddRange(templates);
        await context.Db.SaveChangesAsync(cancellationToken);
        return new BackupImportOutcome(templates.Count, warnings);
    }
}
