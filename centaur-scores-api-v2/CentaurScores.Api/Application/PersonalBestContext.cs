using CentaurScores.Api.Contracts;
using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

public interface IPersonalBestContext
{
    // Walks the tenant and its ancestors (self first) for the nearest one with PersonalBestEnabled set;
    // null if the feature isn't enabled anywhere in the chain.
    Task<Guid?> ResolveOwningTenantIdAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<PersonalBestStatus> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken);
}

public sealed class PersonalBestContext(ApplicationDbContext db) : IPersonalBestContext
{
    public async Task<Guid?> ResolveOwningTenantIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var currentId = tenantId;
        while (true)
        {
            var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(item => item.Id == currentId, cancellationToken);
            if (tenant is null) return null;
            if (tenant.PersonalBestEnabled) return tenant.Id;
            if (tenant.ParentTenantId is not { } parentId) return null;
            currentId = parentId;
        }
    }

    public async Task<PersonalBestStatus> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var owningTenantId = await ResolveOwningTenantIdAsync(tenantId, cancellationToken);
        return new PersonalBestStatus(owningTenantId is not null, owningTenantId == tenantId, owningTenantId);
    }
}
