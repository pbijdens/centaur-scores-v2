using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class PublicScoresController(ApplicationDbContext db, ILiveScoringService liveScoringService, IScorekeeperService? scorekeeperService = null, ILogger<PublicScoresController>? logger = null) : ControllerBase
{
    private IScorekeeperService ScorekeeperService => scorekeeperService ?? new ScorekeeperService(db);

    [HttpGet("live-scoring/match/{scope}")]
    public async Task<ActionResult<IReadOnlyList<LiveScoringMatch>>> LiveScoringMatches(string scope, CancellationToken cancellationToken)
    {
        var matches = await db.Matches.AsNoTracking()
            .Where(match => match.IsOpen && match.LiveScopes.Any(item => item.Scope == scope))
            .OrderBy(match => match.Date)
            .ThenBy(match => match.Name)
            .Select(match => new LiveScoringMatch(match.Id, match.Date, match.Name))
            .ToListAsync(cancellationToken);
        return Ok(matches);
    }

    [HttpGet("live-scoring/match/{scope}/{matchId:guid}")]
    public async Task<ActionResult<LiveScoringPage>> LiveScoringPage(string scope, Guid matchId, CancellationToken cancellationToken)
    {
        var match = await db.Matches.AsNoTracking()
            .Include(item => item.Participants).ThenInclude(item => item.Scores)
            .Include(item => item.LiveScopes)
            .SingleOrDefaultAsync(item => item.Id == matchId && item.IsOpen, cancellationToken);
        var liveScope = match?.LiveScopes.SingleOrDefault(item => item.Scope == scope);
        if (match is null || liveScope is null) return NotFound();

        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(item => item.Id == match.TenantId, cancellationToken);
        if (tenant is null) return NotFound();
        var categories = await db.Categories.AsNoTracking().Include(item => item.Values).Where(item => item.TenantId == match.TenantId).ToListAsync(cancellationToken);
        return Ok(new LiveScoringPage(15, tenant.LogoUrl, tenant.Name, match.Name, match.Date, match.Ends, match.ArrowsPerEnd, match.GroupEnds, liveScoringService.BuildBlocks(match, liveScope, categories)));
    }

    [HttpGet("scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}")]
    public async Task<IActionResult> Scorekeeper(Guid tenantId, Guid matchId, Guid deviceId, CancellationToken cancellationToken)
    {
        var context = await LoadScorekeeperContext(tenantId, matchId, deviceId, cancellationToken);
        if (context is null) return NotFound();
        if (!context.Match.IsOpen) return MatchNoLongerActive();
        LogCall(nameof(Scorekeeper), tenantId, matchId, deviceId);
        return Ok(await ScorekeeperService.GetMatchAsync(context, cancellationToken));
    }

    [HttpPut("scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}/participants")]
    public async Task<IActionResult> SetScorekeeperParticipants(Guid tenantId, Guid matchId, Guid deviceId, IReadOnlyList<ScorekeeperParticipantRequest> request, CancellationToken cancellationToken)
    {
        var context = await LoadScorekeeperContext(tenantId, matchId, deviceId, cancellationToken);
        if (context is null) return NotFound();
        if (!context.Match.IsOpen) return MatchNoLongerActive();
        LogCall(nameof(SetScorekeeperParticipants), tenantId, matchId, deviceId, request);
        var error = await ScorekeeperService.SetParticipantsAsync(context, request, cancellationToken);
        return error is null ? NoContent() : Conflict(error);
    }

    [HttpPut("scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}/scores")]
    public async Task<IActionResult> UpdateScorekeeperScores(Guid tenantId, Guid matchId, Guid deviceId, IReadOnlyList<ScorekeeperScoreUpdates> request, CancellationToken cancellationToken)
    {
        var context = await LoadScorekeeperContext(tenantId, matchId, deviceId, cancellationToken);
        if (context is null) return NotFound();
        if (!context.Match.IsOpen) return MatchNoLongerActive();
        LogCall(nameof(UpdateScorekeeperScores), tenantId, matchId, deviceId, request);
        var conflicts = await ScorekeeperService.UpdateScoresAsync(context, request, cancellationToken);
        return conflicts.Count == 0 ? NoContent() : Conflict(new { error = new ApiError("UPDATE_SCORE_CONFLICT", "One or more score updates conflicted."), conflicts });
    }

    [HttpGet("scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}/participant-options")]
    public async Task<IActionResult> ScorekeeperParticipantOptions(Guid tenantId, Guid matchId, Guid deviceId, CancellationToken cancellationToken)
    {
        var context = await LoadScorekeeperContext(tenantId, matchId, deviceId, cancellationToken);
        if (context is null) return NotFound();
        if (!context.Match.IsOpen) return MatchNoLongerActive();
        LogCall(nameof(ScorekeeperParticipantOptions), tenantId, matchId, deviceId);
        return Ok(await ScorekeeperService.GetParticipantOptionsAsync(context, cancellationToken));
    }

    [HttpGet("scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}/time")]
    public async Task<IActionResult> ScorekeeperTime(Guid tenantId, Guid matchId, Guid deviceId, CancellationToken cancellationToken)
    {
        var context = await LoadScorekeeperContext(tenantId, matchId, deviceId, cancellationToken);
        if (context is null) return NotFound();
        if (!context.Match.IsOpen) return MatchNoLongerActive();
        LogCall(nameof(ScorekeeperTime), tenantId, matchId, deviceId);
        await Task.CompletedTask;
        return Ok(new ScorekeeperTime(DateTimeOffset.UtcNow));
    }

    private async Task<ScorekeeperContext?> LoadScorekeeperContext(Guid tenantId, Guid matchId, Guid deviceId, CancellationToken cancellationToken) => await ScorekeeperService.FindAsync(tenantId, matchId, deviceId, cancellationToken);
    private IActionResult MatchNoLongerActive() => Conflict(new ApiError("MATCH_NO_LONGER_ACTIVE", "The match is no longer active."));
    private void LogCall(string operation, Guid tenantId, Guid matchId, Guid deviceId, object? parameters = null) => logger?.LogInformation("Public scorekeeper call {Operation} from {IpAddress}: tenantId={TenantId}, matchId={MatchId}, deviceId={DeviceId}, parameters={Parameters}", operation, HttpContext.Connection.RemoteIpAddress?.ToString(), tenantId, matchId, deviceId, parameters);
}