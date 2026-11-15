using CentaurScores.Api.Contracts;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

// Supports the web UI's "federation number changed" flow on a participant list member: before saving, the UI
// asks which personal-best entries still carry the member's current number, and the admin may then choose to
// move them to the new number as part of the save. Personal-best data lives in the owning tenant (possibly a
// super tenant), resolved from the member's tenant. API callers changing a number get no automatic action.
public interface IPersonalBestNumberChanges
{
    Task<MemberPersonalBestEntries> PreviewAsync(Guid tenantId, string? federationNumber, int limit, CancellationToken cancellationToken);

    // Moves the log entries, the archer name and open import conflicts from oldNumber to newNumber. Does not save.
    Task<int> RenumberAsync(Guid tenantId, string oldNumber, string newNumber, CancellationToken cancellationToken);
}

public sealed class PersonalBestNumberChanges(ApplicationDbContext db, IPersonalBestContext personalBestContext) : IPersonalBestNumberChanges
{
    public async Task<MemberPersonalBestEntries> PreviewAsync(Guid tenantId, string? federationNumber, int limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(federationNumber)) return new(federationNumber, 0, []);
        var owningTenantId = await personalBestContext.ResolveOwningTenantIdAsync(tenantId, cancellationToken);
        if (owningTenantId is null) return new(federationNumber, 0, []);

        var query = db.PersonalBestLogEntries.AsNoTracking().Where(item => item.TenantId == owningTenantId && item.FederationNumber == federationNumber);
        var total = await query.CountAsync(cancellationToken);
        var recent = await query
            .OrderByDescending(item => item.Date).ThenByDescending(item => item.RecordedAt)
            .Take(limit)
            .Select(item => new PersonalBestEntryPreview(item.Date, item.Score, item.Discipline, item.MatchClassifier))
            .ToListAsync(cancellationToken);
        return new(federationNumber, total, recent);
    }

    public async Task<int> RenumberAsync(Guid tenantId, string oldNumber, string newNumber, CancellationToken cancellationToken)
    {
        var owningTenantId = await personalBestContext.ResolveOwningTenantIdAsync(tenantId, cancellationToken);
        if (owningTenantId is null) return 0;

        var entries = await db.PersonalBestLogEntries.Where(item => item.TenantId == owningTenantId && item.FederationNumber == oldNumber).ToListAsync(cancellationToken);
        foreach (var entry in entries) entry.FederationNumber = newNumber;

        // One canonical name per number: keep the target's name if it already has one, else carry the old one over.
        var oldName = await db.PersonalBestArcherNames.SingleOrDefaultAsync(item => item.TenantId == owningTenantId && item.FederationNumber == oldNumber, cancellationToken);
        if (oldName is not null)
        {
            var newNameExists = await db.PersonalBestArcherNames.AnyAsync(item => item.TenantId == owningTenantId && item.FederationNumber == newNumber, cancellationToken);
            if (newNameExists) db.PersonalBestArcherNames.Remove(oldName);
            else oldName.FederationNumber = newNumber;
        }

        var conflicts = await db.PersonalBestImportConflicts.Where(item => item.TenantId == owningTenantId && item.FederationNumber == oldNumber).ToListAsync(cancellationToken);
        foreach (var conflict in conflicts) conflict.FederationNumber = newNumber;

        return entries.Count;
    }
}
