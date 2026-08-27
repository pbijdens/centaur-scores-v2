using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Controllers;

[Route("api/participant-lists")]
public sealed class ParticipantListsController(ApplicationDbContext db, ITenantContext tenantContext, IParticipantListExcelService excelService) : ApiControllerBase(tenantContext)
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

    [HttpDelete("{listId:guid}")]
    public async Task<IActionResult> Delete(Guid listId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var list = await db.ParticipantLists.SingleOrDefaultAsync(item => item.Id == listId && item.TenantId == TenantId, cancellationToken);
        if (list is null) return NotFound();
        db.ParticipantLists.Remove(list);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
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

    [HttpGet("{listId:guid}/export.xlsx")]
    public async Task<IActionResult> Export(Guid listId, [FromQuery] string language = "en", CancellationToken cancellationToken = default)
    {
        if (!CanManage) return Forbid();
        var list = await db.ParticipantLists.AsNoTracking().Include(item => item.Members).SingleOrDefaultAsync(item => item.Id == listId && item.TenantId == TenantId, cancellationToken);
        if (list is null) return NotFound();
        var categories = await db.Categories.AsNoTracking().Include(item => item.Values).Where(item => item.TenantId == TenantId).ToListAsync(cancellationToken);
        var bytes = excelService.Export(list, categories, language);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{list.Name}.xlsx");
    }

    [HttpPost("{listId:guid}/import.xlsx")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Import(Guid listId, IFormFile file, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var list = await db.ParticipantLists.Include(item => item.Members).SingleOrDefaultAsync(item => item.Id == listId && item.TenantId == TenantId, cancellationToken);
        if (list is null) return NotFound();
        if (file.Length == 0) return BadRequest(new ApiError("IMPORT_FILE_MISSING", "No file was uploaded."));

        var categories = await db.Categories.AsNoTracking().Include(item => item.Values).Where(item => item.TenantId == TenantId).ToListAsync(cancellationToken);

        ParticipantListImportParseResult parsed;
        try
        {
            using var stream = file.OpenReadStream();
            parsed = excelService.Import(stream, categories);
        }
        catch (ParticipantListImportException exception)
        {
            return BadRequest(new ApiError(exception.Code, exception.Message));
        }

        // Reconcile by federation number: a matching number updates the existing member, anything else is
        // added as new. Members missing from the file are left untouched - import never deletes.
        var membersByNumber = list.Members
            .Where(member => !string.IsNullOrWhiteSpace(member.FederationNumber))
            .GroupBy(member => member.FederationNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var updated = 0;
        foreach (var row in parsed.Rows)
        {
            var existing = row.FederationNumber is not null && membersByNumber.TryGetValue(row.FederationNumber, out var found) ? found : null;
            if (existing is not null)
            {
                existing.FullName = row.FullName;
                existing.LastName = row.LastName;
                existing.FederationNumber = row.FederationNumber;
                existing.Categories = new Dictionary<Guid, int>(row.Categories);
                existing.IsActive = row.IsActive;
                updated++;
            }
            else
            {
                db.ParticipantListMembers.Add(new ParticipantListMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    ParticipantListId = listId,
                    FullName = row.FullName,
                    LastName = row.LastName,
                    FederationNumber = row.FederationNumber,
                    Categories = new Dictionary<Guid, int>(row.Categories),
                    IsActive = row.IsActive
                });
                created++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new ImportParticipantListResult(created, updated, parsed.Warnings));
    }
}