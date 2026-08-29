using System.IO.Compression;
using System.Text.Json;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

/// <summary>Thrown for backup-shaped problems that should surface as a coded 400 to the caller (see ApiError).</summary>
public sealed class BackupRestoreException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public interface IRestoreService
{
    Task<RestoreBackupResult> RestoreAsync(Guid adminTenantId, Stream zipStream, CancellationToken cancellationToken);
}

public sealed class RestoreService(ApplicationDbContext db) : IRestoreService
{
    public async Task<RestoreBackupResult> RestoreAsync(Guid adminTenantId, Stream zipStream, CancellationToken cancellationToken)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch (InvalidDataException)
        {
            throw new BackupRestoreException("RESTORE_INVALID_FILE", "The uploaded file is not a valid ZIP archive.");
        }

        using (archive)
        {
            var index = ReadIndex(archive);
            if (index.FormatVersion != BackupService.FormatVersion)
            {
                throw new BackupRestoreException("RESTORE_UNSUPPORTED_VERSION", $"This backup was created with format version {index.FormatVersion}, which is not supported by this server.");
            }

            var usernamePrefix = GenerateUsernamePrefix();
            var context = new BackupImportContext(db, archive, adminTenantId, index.RootTenantId, usernamePrefix);
            if (index.DroppedPersonalBestDisciplineMappings > 0)
            {
                context.Warnings.Add($"{index.DroppedPersonalBestDisciplineMappings} personal-best discipline mapping(s) were excluded from this backup because they referenced a tenant outside the exported set.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            foreach (var handler in BackupService.Handlers)
            {
                var outcome = await handler.ImportAsync(context, cancellationToken);
                context.Warnings.AddRange(outcome.Warnings);
            }

            if (!context.TryRemap(index.RootTenantId, out var newRootTenantId))
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new BackupRestoreException("RESTORE_INVALID_ROOT_TENANT", "The backup's root tenant could not be restored.");
            }

            await transaction.CommitAsync(cancellationToken);

            var newRootTenant = await db.Tenants.AsNoTracking().SingleAsync(item => item.Id == newRootTenantId, cancellationToken);
            return new RestoreBackupResult(newRootTenantId, newRootTenant.Name, context.Warnings);
        }
    }

    private static BackupIndex ReadIndex(ZipArchive archive)
    {
        var indexEntry = archive.GetEntry("index.json")
            ?? throw new BackupRestoreException("RESTORE_MISSING_INDEX", "The uploaded file is not a valid backup (index.json is missing).");
        using var stream = indexEntry.Open();
        try
        {
            return JsonSerializer.Deserialize<BackupIndex>(stream) ?? throw new BackupRestoreException("RESTORE_INVALID_INDEX", "The uploaded file's index.json could not be read.");
        }
        catch (JsonException)
        {
            throw new BackupRestoreException("RESTORE_INVALID_INDEX", "The uploaded file's index.json could not be read.");
        }
    }

    private static string GenerateUsernamePrefix()
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(6);
        var chars = bytes.Select(item => alphabet[item % alphabet.Length]).ToArray();
        return $"r-{new string(chars)}-";
    }
}
