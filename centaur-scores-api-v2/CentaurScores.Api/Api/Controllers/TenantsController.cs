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
public sealed class TenantsController(ApplicationDbContext db, ITenantContext tenantContext) : ApiControllerBase(tenantContext)
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
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = request.Name, LogoUrl = request.LogoUrl, ParentTenantId = request.ParentTenantId };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Current), new { id = tenant.Id }, tenant);
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