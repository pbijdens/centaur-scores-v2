using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Application;

public interface INarrowcastScopeContext
{
    // Walks the tenant and its ancestors (self first) for the nearest one with DefaultNarrowcastScope set;
    // falls back to "all" if none in the chain configured one.
    Task<string> ResolveEffectiveScopeAsync(Guid tenantId, CancellationToken cancellationToken);
}

public sealed class NarrowcastScopeContext(ApplicationDbContext db) : INarrowcastScopeContext
{
    public const string DefaultScope = "all";

    public async Task<string> ResolveEffectiveScopeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var currentId = tenantId;
        while (true)
        {
            var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(item => item.Id == currentId, cancellationToken);
            if (tenant is null) return DefaultScope;
            if (tenant.DefaultNarrowcastScope is { Length: > 0 } scope) return scope;
            if (tenant.ParentTenantId is not { } parentId) return DefaultScope;
            currentId = parentId;
        }
    }
}
