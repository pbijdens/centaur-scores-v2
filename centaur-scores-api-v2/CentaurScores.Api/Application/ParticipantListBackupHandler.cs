using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

public sealed class ParticipantListBackupHandler : IBackupHandler
{
    public string FolderName => "participant-lists";

    public async Task<IReadOnlyList<(Guid, object)>> ExportAsync(ApplicationDbContext db, IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var lists = await db.ParticipantLists.AsNoTracking().Include(item => item.Members).Where(item => tenantIds.Contains(item.TenantId)).ToListAsync(cancellationToken);
        return lists.Select(item => (item.Id, (object)new ParticipantListBackup(
            item.Id, item.TenantId, item.Name, item.IsActive,
            item.Members.Select(member => new ParticipantListMemberBackup(member.Id, member.LastName, member.FullName, member.FederationNumber, member.Categories, member.IsActive)).ToList()))).ToList();
    }

    public async Task<BackupImportOutcome> ImportAsync(BackupImportContext context, CancellationToken cancellationToken)
    {
        var entries = context.ReadEntries<ParticipantListBackup>(FolderName);
        var warnings = new List<string>();
        var lists = new List<ParticipantList>();

        foreach (var entry in entries)
        {
            if (!context.TryRemap(entry.TenantId, out var newTenantId))
            {
                warnings.Add($"Skipped participant list '{entry.Name}': its owning tenant was not part of this backup.");
                continue;
            }

            var newListId = context.Mint(entry.Id);
            var members = entry.Members.Select(member => new ParticipantListMember
            {
                Id = context.Mint(member.Id),
                TenantId = newTenantId,
                ParticipantListId = newListId,
                LastName = member.LastName,
                FullName = member.FullName,
                FederationNumber = member.FederationNumber,
                Categories = BackupRemapHelpers.RemapCategoryDictionary(member.Categories, context),
                IsActive = member.IsActive
            }).ToList();

            lists.Add(new ParticipantList { Id = newListId, TenantId = newTenantId, Name = entry.Name, IsActive = entry.IsActive, Members = members });
        }

        context.Db.ParticipantLists.AddRange(lists);
        await context.Db.SaveChangesAsync(cancellationToken);
        return new BackupImportOutcome(lists.Count, warnings);
    }
}
