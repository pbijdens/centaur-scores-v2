using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

public interface IBackupService
{
    Task<(byte[] ZipBytes, string FileName)> CreateBackupAsync(Guid rootTenantId, bool includeSubTenants, CancellationToken cancellationToken);
}

public sealed class BackupService(ApplicationDbContext db) : IBackupService
{
    public const int FormatVersion = 1;

    // Registration order is also the restore dependency order (see RestoreService): a tenant's accounts,
    // categories, participant lists, etc. must all resolve before anything that references them.
    public static readonly IReadOnlyList<IBackupHandler> Handlers =
    [
        new TenantBackupHandler(),
        new AccountBackupHandler(),
        new CategoryBackupHandler(),
        new ParticipantListBackupHandler(),
        new MatchTemplateBackupHandler(),
        new MatchBackupHandler(),
        new CompetitionBackupHandler(),
        new PersonalBestInfoBackupHandler()
    ];

    public async Task<(byte[], string)> CreateBackupAsync(Guid rootTenantId, bool includeSubTenants, CancellationToken cancellationToken)
    {
        var rootTenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(item => item.Id == rootTenantId, cancellationToken)
            ?? throw new InvalidOperationException("The current tenant could not be found.");
        var tenantIds = await ResolveTenantTreeAsync(rootTenantId, includeSubTenants, cancellationToken);

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var handler in Handlers)
            {
                var entries = await handler.ExportAsync(db, tenantIds, cancellationToken);
                foreach (var (id, payload) in entries)
                {
                    WriteJsonEntry(archive, $"{handler.FolderName}/{id}.json", payload);
                }
            }

            var droppedMappings = await db.PersonalBestDisciplineMappings.AsNoTracking()
                .Where(item => tenantIds.Contains(item.TenantId) && !tenantIds.Contains(item.SourceTenantId))
                .CountAsync(cancellationToken);

            var index = new BackupIndex(FormatVersion, DateTime.UtcNow, rootTenantId, tenantIds.ToList(), includeSubTenants, droppedMappings, typeof(BackupService).Assembly.GetName().Version?.ToString() ?? "0.0.0");
            WriteJsonEntry(archive, "index.json", index);
        }

        var fileName = $"centaur-backup-{Sanitize(rootTenant.Name)}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
        return (memoryStream.ToArray(), fileName);
    }

    private async Task<IReadOnlyList<Guid>> ResolveTenantTreeAsync(Guid rootTenantId, bool includeSubTenants, CancellationToken cancellationToken)
    {
        var tenantIds = new List<Guid> { rootTenantId };
        if (!includeSubTenants) return tenantIds;

        var visited = new HashSet<Guid> { rootTenantId };
        var frontier = new List<Guid> { rootTenantId };
        while (frontier.Count > 0)
        {
            var children = await db.Tenants.AsNoTracking()
                .Where(item => item.ParentTenantId != null && frontier.Contains(item.ParentTenantId!.Value))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
            frontier = children.Where(visited.Add).ToList();
            tenantIds.AddRange(frontier);
        }
        return tenantIds;
    }

    private static void WriteJsonEntry(ZipArchive archive, string entryName, object payload)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        JsonSerializer.Serialize(entryStream, payload, payload.GetType());
    }

    private static string Sanitize(string name)
    {
        var builder = new StringBuilder();
        foreach (var character in name) builder.Append(char.IsLetterOrDigit(character) ? character : '-');
        return builder.Length == 0 ? "tenant" : builder.ToString();
    }
}
