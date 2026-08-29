using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

public sealed class AccountBackupHandler : IBackupHandler
{
    public string FolderName => "accounts";

    public async Task<IReadOnlyList<(Guid, object)>> ExportAsync(ApplicationDbContext db, IReadOnlyList<Guid> tenantIds, CancellationToken cancellationToken)
    {
        var accounts = await db.Accounts.AsNoTracking().Where(item => tenantIds.Contains(item.TenantId)).ToListAsync(cancellationToken);
        return accounts.Select(item => (item.Id, (object)new AccountBackup(item.Id, item.TenantId, item.Username, item.PasswordHash, item.DisplayName, item.Email, item.Authorization.ToString()))).ToList();
    }

    public async Task<BackupImportOutcome> ImportAsync(BackupImportContext context, CancellationToken cancellationToken)
    {
        var entries = context.ReadEntries<AccountBackup>(FolderName);
        var warnings = new List<string>();
        var accounts = new List<Account>();

        foreach (var entry in entries)
        {
            if (!context.TryRemap(entry.TenantId, out var newTenantId))
            {
                warnings.Add($"Skipped account '{entry.Username}': its owning tenant was not part of this backup.");
                continue;
            }

            var authorization = Enum.TryParse<AuthorizationProfile>(entry.Authorization, out var parsed) ? parsed : AuthorizationProfile.Viewer;
            accounts.Add(new Account
            {
                Id = context.Mint(entry.Id),
                TenantId = newTenantId,
                Username = context.UsernamePrefix + entry.Username,
                PasswordHash = entry.PasswordHash,
                DisplayName = entry.DisplayName,
                Email = entry.Email,
                Authorization = authorization
            });
        }

        context.Db.Accounts.AddRange(accounts);
        await context.Db.SaveChangesAsync(cancellationToken);
        return new BackupImportOutcome(accounts.Count, warnings);
    }
}
