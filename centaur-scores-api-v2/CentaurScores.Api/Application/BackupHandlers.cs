using System.IO.Compression;
using System.Text.Json;
using CentaurScores.Api.Infrastructure;

namespace CentaurScores.Api.Application;

/// <summary>
/// One implementation per entity family (tenants, categories, matches, ...). Export reads rows scoped to a
/// tenant set and serializes them to a DTO; import reads the matching JSON files back out of the archive,
/// mints fresh ids, remaps every reference through the shared <see cref="BackupImportContext.IdMap"/>, and
/// persists the result. New entity types are added by writing one more handler and appending it to
/// <see cref="BackupService.Handlers"/> - existing handlers never need to change.
/// </summary>
public interface IBackupHandler
{
    string FolderName { get; }
    Task<IReadOnlyList<(Guid Id, object Payload)>> ExportAsync(ApplicationDbContext db, IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken);
    Task<BackupImportOutcome> ImportAsync(BackupImportContext context, CancellationToken cancellationToken);
}

public sealed record BackupImportOutcome(int Created, IReadOnlyList<string> Warnings);

/// <summary>
/// Shared state for one restore run: the archive being read, the running original-id-to-new-id map (spans
/// every entity type, including tenants), and the account-username prefix/new-parent-tenant generated once
/// for the whole run. A remap lookup that misses means the referenced object was outside the backup's scope
/// (e.g. a personal-best mapping pointing at a tenant that wasn't exported) - handlers must drop the
/// referencing row/field and record a warning rather than ever writing a raw pre-existing id.
/// </summary>
public sealed class BackupImportContext(ApplicationDbContext db, ZipArchive archive, Guid adminTenantId, Guid rootTenantOriginalId, string usernamePrefix)
{
    public ApplicationDbContext Db { get; } = db;
    public ZipArchive Archive { get; } = archive;
    public Dictionary<Guid, Guid> IdMap { get; } = [];
    public Guid AdminTenantId { get; } = adminTenantId;
    public Guid RootTenantOriginalId { get; } = rootTenantOriginalId;
    public string UsernamePrefix { get; } = usernamePrefix;
    public List<string> Warnings { get; } = [];

    public Guid Mint(Guid originalId)
    {
        var newId = Guid.NewGuid();
        IdMap[originalId] = newId;
        return newId;
    }

    public bool TryRemap(Guid originalId, out Guid newId) => IdMap.TryGetValue(originalId, out newId);

    public List<T> ReadEntries<T>(string folderName)
    {
        var prefix = folderName + "/";
        var results = new List<T>();
        foreach (var entry in Archive.Entries
            .Where(item => item.FullName.StartsWith(prefix, StringComparison.Ordinal) && item.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.FullName, StringComparer.Ordinal))
        {
            using var stream = entry.Open();
            var value = JsonSerializer.Deserialize<T>(stream);
            if (value is not null) results.Add(value);
        }
        return results;
    }
}

public static class BackupRemapHelpers
{
    public static Dictionary<Guid, int> RemapCategoryDictionary(IReadOnlyDictionary<Guid, int> source, BackupImportContext context)
    {
        var result = new Dictionary<Guid, int>();
        foreach (var (categoryId, valueId) in source)
        {
            if (context.TryRemap(categoryId, out var newCategoryId)) result[newCategoryId] = valueId;
        }
        return result;
    }

    public static string RemapGuidJsonArray(string json, BackupImportContext context)
    {
        List<Guid>? ids;
        try { ids = JsonSerializer.Deserialize<List<Guid>>(json); }
        catch (JsonException) { ids = null; }
        var remapped = (ids ?? []).Where(id => context.TryRemap(id, out _)).Select(id => context.IdMap[id]).ToList();
        return JsonSerializer.Serialize(remapped);
    }
}
