using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Controllers;

[Route("api/match-templates")]
public sealed class TemplatesController(ApplicationDbContext db, ITenantContext tenantContext) : ApiControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.MatchTemplates.AsNoTracking().Where(item => item.TenantId == TenantId).OrderBy(item => item.Name).ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CreateTemplateRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var template = new MatchTemplate { Id = Guid.NewGuid(), TenantId = TenantId, Name = request.Name, ParticipantListId = request.ParticipantListId, AllowFreeParticipants = request.AllowFreeParticipants, DeviceSelectionMode = string.IsNullOrWhiteSpace(request.DeviceSelectionMode) ? "list-and-free" : request.DeviceSelectionMode, ConfigurationJson = string.IsNullOrWhiteSpace(request.ConfigurationJson) ? MatchDefaults.KeyboardJson : request.ConfigurationJson, PersonalBestClassifier = request.PersonalBestClassifier };
        db.MatchTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"api/match-templates/{template.Id}", template);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CreateTemplateRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var template = await db.MatchTemplates.FirstOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken);
        if (template is null) return NotFound();
        template.Name = request.Name;
        template.ParticipantListId = request.ParticipantListId;
        template.AllowFreeParticipants = request.AllowFreeParticipants;
        template.DeviceSelectionMode = request.DeviceSelectionMode;
        template.ConfigurationJson = request.ConfigurationJson;
        template.PersonalBestClassifier = request.PersonalBestClassifier;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(template);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        var template = await db.MatchTemplates.SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken);
        if (template is null) return NotFound();
        db.MatchTemplates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}