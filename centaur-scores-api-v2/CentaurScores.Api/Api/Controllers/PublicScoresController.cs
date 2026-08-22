using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class PublicScoresController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet("live-scores/{tenantId:guid}/{scope}")]
    public async Task<IActionResult> LiveScores(Guid tenantId, string scope, CancellationToken cancellationToken) => Ok(new { tenantId, scope, generatedAt = DateTimeOffset.UtcNow, matches = await db.Matches.AsNoTracking().Include(item => item.Participants).ThenInclude(item => item.Scores).Where(item => item.TenantId == tenantId && item.IsOpen).ToListAsync(cancellationToken) });

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