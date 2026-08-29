using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

public sealed class CategoryBackupHandler : IBackupHandler
{
    public string FolderName => "categories";

    public async Task<IReadOnlyList<(Guid, object)>> ExportAsync(ApplicationDbContext db, IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var categories = await db.Categories.AsNoTracking().Include(item => item.Values).Where(item => tenantIds.Contains(item.TenantId)).ToListAsync(cancellationToken);
        return categories.Select(item => (item.Id, (object)new CategoryBackup(
            item.Id, item.TenantId, item.Name, item.IsUsed,
            item.Values.Select(value => new CategoryValueBackup(value.ValueId, value.Name)).ToList()))).ToList();
    }

    public async Task<BackupImportOutcome> ImportAsync(BackupImportContext context, CancellationToken cancellationToken)
    {
        var entries = context.ReadEntries<CategoryBackup>(FolderName);
        var warnings = new List<string>();
        var categories = new List<Category>();

        foreach (var entry in entries)
        {
            if (!context.TryRemap(entry.TenantId, out var newTenantId))
            {
                warnings.Add($"Skipped category '{entry.Name}': its owning tenant was not part of this backup.");
                continue;
            }

            var newCategoryId = context.Mint(entry.Id);
            categories.Add(new Category
            {
                Id = newCategoryId,
                TenantId = newTenantId,
                Name = entry.Name,
                IsUsed = entry.IsUsed,
                Values = entry.Values.Select(value => new CategoryValue { Id = Guid.NewGuid(), TenantId = newTenantId, CategoryId = newCategoryId, ValueId = value.ValueId, Name = value.Name }).ToList()
            });
        }

        context.Db.Categories.AddRange(categories);
        await context.Db.SaveChangesAsync(cancellationToken);
        return new BackupImportOutcome(categories.Count, warnings);
    }
}
