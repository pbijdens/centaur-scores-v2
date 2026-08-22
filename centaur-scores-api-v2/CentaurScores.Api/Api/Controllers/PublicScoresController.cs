using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class PublicScoresController(ApplicationDbContext db, ILiveScoringService liveScoringService) : ControllerBase
{
    [HttpGet("live-scoring/match/{tenantId:guid}/{scope}")]
    public async Task<ActionResult<IReadOnlyList<LiveScoringMatch>>> LiveScoringMatches(Guid tenantId, string scope, CancellationToken cancellationToken)
    {
        var matches = await db.Matches.AsNoTracking()
            .Where(match => match.TenantId == tenantId && match.IsOpen && match.LiveScopes.Any(item => item.Scope == scope))
            .OrderBy(match => match.Date)
            .ThenBy(match => match.Name)
            .Select(match => new LiveScoringMatch(match.Id, match.Date, match.Name))
            .ToListAsync(cancellationToken);
        return Ok(matches);
    }

    [HttpGet("live-scoring/match/{tenantId:guid}/{scope}/{matchId:guid}")]
    public async Task<ActionResult<LiveScoringPage>> LiveScoringPage(Guid tenantId, string scope, Guid matchId, CancellationToken cancellationToken)
    {
        var match = await db.Matches.AsNoTracking()
            .Include(item => item.Participants).ThenInclude(item => item.Scores)
            .Include(item => item.LiveScopes)
            .SingleOrDefaultAsync(item => item.Id == matchId && item.TenantId == tenantId && item.IsOpen, cancellationToken);
        var liveScope = match?.LiveScopes.SingleOrDefault(item => item.Scope == scope);
        if (match is null || liveScope is null) return NotFound();

        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(item => item.Id == tenantId, cancellationToken);
        if (tenant is null) return NotFound();
        var categories = await db.Categories.AsNoTracking().Include(item => item.Values).Where(item => item.TenantId == tenantId).ToListAsync(cancellationToken);
        return Ok(new LiveScoringPage(15, tenant.LogoUrl, tenant.Name, match.Name, match.Date, liveScoringService.BuildBlocks(match, liveScope, categories)));
    }

    [HttpGet("scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}")]
    public async Task<IActionResult> Scorekeeper(Guid tenantId, Guid matchId, Guid deviceId, CancellationToken cancellationToken)
    {
        var match = await db.Matches.AsNoTracking().Include(item => item.Participants).Include(item => item.Devices).SingleOrDefaultAsync(item => item.Id == matchId && item.TenantId == tenantId, cancellationToken);
        if (match is null) return NotFound();
        var participants = match.Participants
            .Where(item => item.DeviceId == deviceId || item.DeviceId == null)
            .OrderBy(item => item.DeviceId == deviceId ? 0 : 1)
            .ThenBy(item => item.DeviceId == deviceId ? item.DeviceOrder : int.MaxValue)
            .ThenBy(item => item.LastName)
            .ToList();
        return Ok(new { match, device = match.Devices.SingleOrDefault(item => item.Id == deviceId), participants });
    }
}