using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

public sealed class TenantBackupHandler : IBackupHandler
{
    public string FolderName => "tenants";

    public async Task<IReadOnlyList<(Guid, object)>> ExportAsync(ApplicationDbContext db, IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var tenants = await db.Tenants.AsNoTracking().Where(item => tenantIds.Contains(item.Id)).ToListAsync(cancellationToken);
        return tenants.Select(item => (item.Id, (object)new TenantBackup(item.Id, item.Name, item.LogoUrl, item.ParentTenantId, item.PersonalBestEnabled))).ToList();
    }

    public async Task<BackupImportOutcome> ImportAsync(BackupImportContext context, CancellationToken cancellationToken)
    {
        var entries = context.ReadEntries<TenantBackup>(FolderName);
        var warnings = new List<string>();

        // Mint every tenant's new id first, so the second pass can resolve any tenant's ParentTenantId
        // regardless of which order the tenants happen to appear in the archive.
        var newTenants = entries.Select(entry => new Tenant { Id = context.Mint(entry.Id), Name = entry.Name, LogoUrl = entry.LogoUrl, PersonalBestEnabled = entry.PersonalBestEnabled }).ToList();

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var tenant = newTenants[index];
            if (entry.Id == context.RootTenantOriginalId)
            {
                tenant.Name = $"{entry.Name} (restored {DateTime.UtcNow:yyyy-MM-dd HH:mm})";
                tenant.ParentTenantId = context.AdminTenantId;
            }
            else if (entry.ParentTenantId is { } parentId && context.TryRemap(parentId, out var newParentId))
            {
                tenant.ParentTenantId = newParentId;
            }
            else
            {
                // Should not happen: every non-root tenant in the export was reached by walking down from
                // the root, so its parent is always in this same set. Fail safe rather than orphan it silently.
                warnings.Add($"Tenant '{entry.Name}' had no resolvable parent in the backup; attached directly under the admin's current tenant.");
                tenant.ParentTenantId = context.AdminTenantId;
            }
        }

        context.Db.Tenants.AddRange(newTenants);
        await context.Db.SaveChangesAsync(cancellationToken);
        return new BackupImportOutcome(newTenants.Count, warnings);
    }
}
