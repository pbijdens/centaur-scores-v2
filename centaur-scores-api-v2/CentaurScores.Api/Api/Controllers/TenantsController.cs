using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Controllers;

[ApiController]
[Route("api/tenants")]
public sealed class TenantsController(ApplicationDbContext db, ITenantContext tenantContext, INarrowcastScopeContext narrowcastScopeContext) : ApiControllerBase(tenantContext)
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.Tenants.AsNoTracking().OrderBy(item => item.Name).Select(item => new { item.Id, item.Name, item.LogoUrl }).ToListAsync(cancellationToken));

    [HttpGet("current")]
    public async Task<IActionResult> Current(CancellationToken cancellationToken) => Ok(await db.Tenants.AsNoTracking().SingleOrDefaultAsync(item => item.Id == TenantId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = request.Name, LogoUrl = request.LogoUrl, ParentTenantId = request.ParentTenantId, DefaultNarrowcastScope = request.DefaultNarrowcastScope };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Current), new { id = tenant.Id }, tenant);
    }

    [HttpGet("current/default-scope")]
    public async Task<IActionResult> CurrentDefaultScope(CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(item => item.Id == TenantId, cancellationToken);
        if (tenant is null) return NotFound();
        var effective = await narrowcastScopeContext.ResolveEffectiveScopeAsync(TenantId, cancellationToken);
        return Ok(new DefaultScopeSettings(tenant.DefaultNarrowcastScope, effective));
    }

    [HttpPut("current/default-scope")]
    public async Task<IActionResult> UpdateCurrentDefaultScope(UpdateDefaultNarrowcastScopeRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var tenant = await db.Tenants.SingleOrDefaultAsync(item => item.Id == TenantId, cancellationToken);
        if (tenant is null) return NotFound();
        tenant.DefaultNarrowcastScope = request.DefaultNarrowcastScope;
        await db.SaveChangesAsync(cancellationToken);
        var effective = await narrowcastScopeContext.ResolveEffectiveScopeAsync(TenantId, cancellationToken);
        return Ok(new DefaultScopeSettings(tenant.DefaultNarrowcastScope, effective));
    }

    [HttpPut("current")]
    public async Task<IActionResult> Update(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        var tenant = await db.Tenants.SingleOrDefaultAsync(item => item.Id == TenantId, cancellationToken);
        if (tenant is null) return NotFound();
        tenant.Name = request.Name;
        tenant.LogoUrl = request.LogoUrl;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(tenant);
    }

    [HttpGet("children")]
    public async Task<IActionResult> Children(CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        return Ok(await db.Tenants.AsNoTracking().Where(item => item.ParentTenantId == TenantId).OrderBy(item => item.Name).Select(item => new { item.Id, item.Name, item.LogoUrl }).ToListAsync(cancellationToken));
    }

    [HttpGet("children/{id:guid}")]
    public async Task<IActionResult> Child(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.ParentTenantId == TenantId, cancellationToken);
        return tenant is null ? NotFound() : Ok(tenant);
    }

    [HttpPut("children/{id:guid}")]
    public async Task<IActionResult> UpdateChild(Guid id, CreateTenantRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        var tenant = await db.Tenants.SingleOrDefaultAsync(item => item.Id == id && item.ParentTenantId == TenantId, cancellationToken);
        if (tenant is null) return NotFound();
        tenant.Name = request.Name;
        tenant.LogoUrl = request.LogoUrl;
        tenant.DefaultNarrowcastScope = request.DefaultNarrowcastScope;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(tenant);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAdministrator || id == TenantId) return Forbid();
        var tenant = await db.Tenants.SingleOrDefaultAsync(item => item.Id == id && item.ParentTenantId == TenantId, cancellationToken);
        if (tenant is null) return NotFound();
        db.Tenants.Remove(tenant);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}