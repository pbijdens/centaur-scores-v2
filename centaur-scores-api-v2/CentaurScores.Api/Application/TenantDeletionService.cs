using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

public interface ITenantDeletionService
{
    Task DeleteTenantAsync(Guid tenantId, CancellationToken cancellationToken);
}

// TenantId columns carry no real database foreign key (see MEMORY.md's schema-drift entries), so
// TenantsController.Delete previously removed only the Tenant row itself and left every match,
// competition, category, etc. underneath it orphaned. This walks the full descendant-tenant subtree
// (a tenant delete removes its children too, recursively) and explicitly deletes every table's rows
// for that whole tenant set - it does not rely on any database-level cascade, since none of these FKs
// are guaranteed to exist (or to be enforced the same way in every environment/test host).
public sealed class TenantDeletionService(ApplicationDbContext db) : ITenantDeletionService
{
    public async Task DeleteTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenantIds = await CollectSubtreeAsync(tenantId, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // MatchParticipant must go before ParticipantListMember: match_participants.participant_list_member_id
        // -> participant_list_members is a Restrict FK (see ApplicationDbContext), so a member still
        // referenced by a participant can't be deleted first.
        await db.ArrowScores.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.MatchParticipants.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.ScoreDevices.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.LiveScoreScopes.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.Matches.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.MatchTemplates.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.ParticipantListMembers.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.ParticipantLists.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.CategoryValues.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.Categories.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.CompetitionRoundMatches.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.CompetitionScoreRules.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.CompetitionRounds.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.Competitions.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.Accounts.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);

        // Personal Best: TenantId is the *owning* tenant, but a mapping's SourceTenantId/CategoryId can
        // point at a category owned by any descendant of that owner (see Domain.cs). So a mapping can
        // reference one of these tenants via SourceTenantId while being owned (TenantId) by an ancestor
        // tenant that isn't being deleted at all - drop those too, the same way backup/restore drops a
        // mapping whose reference falls outside the operation's scope, rather than leaving it dangling.
        await db.PersonalBestDisciplineMappings.Where(item => tenantIds.Contains(item.TenantId) || tenantIds.Contains(item.SourceTenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.PersonalBestDisciplines.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.PersonalBestClassifiers.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.PersonalBestExportColumns.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.PersonalBestExportConfigs.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.PersonalBestImportConfigs.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.PersonalBestArcherNames.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.PersonalBestLogEntries.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.PersonalBestImportConflicts.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);
        await db.PersonalBestImportBatches.Where(item => tenantIds.Contains(item.TenantId)).ExecuteDeleteAsync(cancellationToken);

        await db.Tenants.Where(item => tenantIds.Contains(item.Id)).ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    // Same down-the-hierarchy walk as AuthService.ResolveAuthorizedTenantsAsync: deleting a tenant deletes
    // every descendant tenant (and their data) too, not just the tenant row itself.
    private async Task<List<Guid>> CollectSubtreeAsync(Guid rootTenantId, CancellationToken cancellationToken)
    {
        var childrenByParent = await db.Tenants.AsNoTracking()
            .Where(item => item.ParentTenantId != null)
            .Select(item => new { item.Id, ParentTenantId = item.ParentTenantId!.Value })
            .ToListAsync(cancellationToken);
        var lookup = childrenByParent.ToLookup(item => item.ParentTenantId, item => item.Id);

        var result = new List<Guid> { rootTenantId };
        var queue = new Queue<Guid>();
        queue.Enqueue(rootTenantId);
        while (queue.Count > 0)
        {
            foreach (var childId in lookup[queue.Dequeue()])
            {
                result.Add(childId);
                queue.Enqueue(childId);
            }
        }
        return result;
    }
}
