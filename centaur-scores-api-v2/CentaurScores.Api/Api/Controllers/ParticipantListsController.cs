using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Controllers;

[Route("api/participant-lists")]
public sealed class ParticipantListsController(ApplicationDbContext db, ITenantContext tenantContext) : ApiControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var query = db.ParticipantLists.AsNoTracking().Include(item => item.Members).Where(item => item.TenantId == TenantId);
        if (!includeInactive) query = query.Where(item => item.IsActive);
        return Ok(await query.OrderByDescending(item => item.IsActive).ThenBy(item => item.Name).ToListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateParticipantListRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var list = new ParticipantList { Id = Guid.NewGuid(), TenantId = TenantId, Name = request.Name, IsActive = request.IsActive };
        db.ParticipantLists.Add(list);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"api/participant-lists/{list.Id}", list);
    }

    [HttpPost("{listId:guid}/members")]
    public async Task<IActionResult> AddMember(Guid listId, CreateParticipantRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!await db.ParticipantLists.AnyAsync(item => item.Id == listId && item.TenantId == TenantId, cancellationToken)) return NotFound();
        var member = new ParticipantListMember { Id = Guid.NewGuid(), TenantId = TenantId, ParticipantListId = listId, LastName = request.LastName, FullName = request.FullName, FederationNumber = request.FederationNumber, Categories = request.Categories, IsActive = request.IsActive };
        db.ParticipantListMembers.Add(member);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"api/participant-lists/{listId}/members/{member.Id}", member);
    }

    [HttpPut("{listId:guid}")]
    public async Task<IActionResult> Update(Guid listId, CreateParticipantListRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var list = await db.ParticipantLists.FirstOrDefaultAsync(item => item.Id == listId && item.TenantId == TenantId, cancellationToken);
        if (list is null) return NotFound();
        list.Name = request.Name;
        list.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(list);
    }

    [HttpPut("{listId:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> UpdateMember(Guid listId, Guid memberId, CreateParticipantRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var member = await db.ParticipantListMembers.SingleOrDefaultAsync(item => item.Id == memberId && item.ParticipantListId == listId && item.TenantId == TenantId, cancellationToken);
        if (member is null) return NotFound();
        member.LastName = request.LastName;
        member.FullName = request.FullName;
        member.FederationNumber = request.FederationNumber;
        member.Categories = request.Categories;
        member.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(member);
    }

    [HttpDelete("{listId:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> DeleteMember(Guid listId, Guid memberId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var member = await db.ParticipantListMembers.SingleOrDefaultAsync(item => item.Id == memberId && item.ParticipantListId == listId && item.TenantId == TenantId, cancellationToken);
        if (member is null) return NotFound();
        db.ParticipantListMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}