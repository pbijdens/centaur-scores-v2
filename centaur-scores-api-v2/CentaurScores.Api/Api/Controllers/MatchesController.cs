using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Controllers;

[Route("api/matches")]
public sealed class MatchesController(ApplicationDbContext db, ITenantContext tenantContext, IScoringService scoring) : ApiControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.Matches.AsNoTracking().Include(item => item.Participants).Where(item => item.TenantId == TenantId).OrderByDescending(item => item.IsOpen).ThenBy(item => item.Date).ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CreateMatchRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var match = new Match { Id = Guid.NewGuid(), TenantId = TenantId, Name = request.Name, Date = request.Date, ShortCode = request.ShortCode, IsOpen = request.IsOpen, ParticipantListId = request.ParticipantListId, DeviceSelectionMode = request.DeviceSelectionMode, Ends = request.Ends, ArrowsPerEnd = request.ArrowsPerEnd, GroupEnds = request.GroupEnds, AllowFreeParticipants = request.AllowFreeParticipants, KeyboardJson = request.KeyboardJson, ScoringRulesJson = request.ScoringRulesJson };
        db.Matches.Add(match);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = match.Id }, match);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) => await db.Matches.AsNoTracking().Include(item => item.Participants).Include(item => item.Devices).Include(item => item.LiveScopes).SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken) is { } match ? Ok(match) : NotFound();

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CreateMatchRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var match = await db.Matches.SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken);
        if (match is null) return NotFound();
        if (match.ParticipantListId != request.ParticipantListId && await db.MatchParticipants.AnyAsync(item => item.MatchId == id, cancellationToken))
            return Conflict(new ApiError("PARTICIPANT_LIST_LOCKED", "The source participant list cannot change once the match has participants."));
        match.Name = request.Name;
        match.Date = request.Date;
        match.ShortCode = request.ShortCode;
        match.IsOpen = request.IsOpen;
        match.ParticipantListId = request.ParticipantListId;
        match.DeviceSelectionMode = request.DeviceSelectionMode;
        match.Ends = request.Ends;
        match.ArrowsPerEnd = request.ArrowsPerEnd;
        match.GroupEnds = request.GroupEnds;
        match.AllowFreeParticipants = request.AllowFreeParticipants;
        match.KeyboardJson = request.KeyboardJson;
        match.ScoringRulesJson = request.ScoringRulesJson;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(match);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        var match = await db.Matches.SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken);
        if (match is null) return NotFound();
        db.Matches.Remove(match);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("deactivate-all")]
    public async Task<IActionResult> DeactivateAll(CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        await db.Matches.Where(item => item.TenantId == TenantId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsOpen, false), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/participants")]
    public async Task<IActionResult> Participants(Guid id, CancellationToken cancellationToken) => Ok(await db.MatchParticipants.AsNoTracking().Include(item => item.Scores).Where(item => item.MatchId == id && item.TenantId == TenantId).OrderBy(item => item.LastName).ToListAsync(cancellationToken));

    [HttpPost("{id:guid}/participants")]
    public async Task<IActionResult> AddParticipant(Guid id, CreateMatchParticipantRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var match = await db.Matches.SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken);
        if (match is null) return NotFound();
        if (request.ParticipantListMemberId is null && !match.AllowFreeParticipants) return BadRequest(new { message = "This match does not allow free participants." });
        if (request.ParticipantListMemberId is { } memberId && await db.MatchParticipants.AnyAsync(item => item.MatchId == id && item.ParticipantListMemberId == memberId, cancellationToken)) return Conflict(new { message = "Participant is already assigned." });
        var participant = new MatchParticipant { Id = Guid.NewGuid(), TenantId = TenantId, MatchId = id, ParticipantListMemberId = request.ParticipantListMemberId, LastName = request.LastName, FullName = request.FullName, FederationNumber = request.FederationNumber, Categories = request.Categories };
        db.MatchParticipants.Add(participant);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"api/matches/{id}/participants/{participant.Id}", participant);
    }

    [HttpPut("{id:guid}/participants/{participantId:guid}")]
    public async Task<IActionResult> UpdateParticipant(Guid id, Guid participantId, CreateMatchParticipantRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var participant = await db.MatchParticipants.SingleOrDefaultAsync(item => item.Id == participantId && item.MatchId == id && item.TenantId == TenantId, cancellationToken);
        if (participant is null) return NotFound();
        if (request.ParticipantListMemberId is { } memberId && await db.MatchParticipants.AnyAsync(item => item.MatchId == id && item.Id != participantId && item.ParticipantListMemberId == memberId, cancellationToken)) return Conflict(new { message = "Participant is already assigned." });
        participant.ParticipantListMemberId = request.ParticipantListMemberId;
        participant.LastName = request.LastName;
        participant.FullName = request.FullName;
        participant.FederationNumber = request.FederationNumber;
        participant.Categories = request.Categories;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(participant);
    }

    [HttpDelete("{id:guid}/participants/{participantId:guid}")]
    public async Task<IActionResult> RemoveParticipant(Guid id, Guid participantId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var participant = await db.MatchParticipants.SingleOrDefaultAsync(item => item.Id == participantId && item.MatchId == id && item.TenantId == TenantId, cancellationToken);
        if (participant is null) return NotFound();
        db.MatchParticipants.Remove(participant);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/participants/{participantId:guid}/scores")]
    public async Task<IActionResult> EnterScore(Guid id, Guid participantId, EnterScoreRequest request, CancellationToken cancellationToken)
    {
        var participant = await db.MatchParticipants.SingleOrDefaultAsync(item => item.Id == participantId && item.MatchId == id && item.TenantId == TenantId, cancellationToken);
        if (participant is null) return NotFound();

        var updated = await db.ArrowScores
            .Where(item => item.MatchParticipantId == participantId && item.End == request.End && item.Arrow == request.Arrow && item.TenantId == TenantId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.KeyId, request.KeyId)
                .SetProperty(item => item.Value, request.Value), cancellationToken);
        if (updated == 0)
        {
            db.ArrowScores.Add(new ArrowScore
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                MatchParticipantId = participantId,
                End = request.End,
                Arrow = request.Arrow,
                KeyId = request.KeyId,
                Value = request.Value
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await db.Entry(participant).Collection(item => item.Scores).LoadAsync(cancellationToken);
        return Ok(scoring.Calculate(participant, (await db.Matches.FindAsync([id], cancellationToken))?.ArrowsPerEnd ?? 1, null));
    }

    [HttpGet("{id:guid}/results")]
    public async Task<IActionResult> Results(Guid id, CancellationToken cancellationToken)
    {
        var match = await db.Matches.Include(item => item.Participants).ThenInclude(item => item.Scores).SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken);
        return match is null ? NotFound() : Ok(scoring.Rank(match.Participants, match));
    }

    [HttpPost("{id:guid}/devices")]
    public async Task<IActionResult> AddDevice(Guid id, CreateDeviceRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!await db.Matches.AnyAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken)) return NotFound();
        var device = new ScoreDevice { Id = Guid.NewGuid(), TenantId = TenantId, MatchId = id, Name = request.Name };
        db.ScoreDevices.Add(device);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"api/matches/{id}/devices/{device.Id}", device);
    }

    [HttpPost("{id:guid}/live-scopes")]
    public async Task<IActionResult> AddScope(Guid id, CreateScopeRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!await db.Matches.AnyAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken)) return NotFound();
        var scope = new LiveScoreScope { Id = Guid.NewGuid(), TenantId = TenantId, MatchId = id, Scope = request.Scope, GroupByCategoryIdsJson = System.Text.Json.JsonSerializer.Serialize(request.GroupByCategoryIds), IncludeAverage = request.IncludeAverage, IncludeGroupScores = request.IncludeGroupScores, IncludeEqualizers = request.IncludeEqualizers, IncludePersonalBest = request.IncludePersonalBest };
        db.LiveScoreScopes.Add(scope);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"api/matches/{id}/live-scopes/{scope.Id}", scope);
    }

    [HttpPut("{id:guid}/participants/{participantId:guid}/device")]
    public async Task<IActionResult> AssignParticipantDevice(Guid id, Guid participantId, AssignParticipantDeviceRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var participant = await db.MatchParticipants.SingleOrDefaultAsync(item => item.Id == participantId && item.MatchId == id && item.TenantId == TenantId, cancellationToken);
        if (participant is null) return NotFound();
        if (request.DeviceId is { } deviceId && !await db.ScoreDevices.AnyAsync(item => item.Id == deviceId && item.MatchId == id && item.TenantId == TenantId, cancellationToken)) return NotFound();
        participant.DeviceId = request.DeviceId;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(participant);
    }

    [HttpDelete("{id:guid}/devices/{deviceId:guid}")]
    public async Task<IActionResult> DeleteDevice(Guid id, Guid deviceId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var device = await db.ScoreDevices.SingleOrDefaultAsync(item => item.Id == deviceId && item.MatchId == id && item.TenantId == TenantId, cancellationToken);
        if (device is null) return NotFound();
        db.ScoreDevices.Remove(device);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/live-scopes/{scopeId:guid}")]
    public async Task<IActionResult> DeleteScope(Guid id, Guid scopeId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var scope = await db.LiveScoreScopes.SingleOrDefaultAsync(item => item.Id == scopeId && item.MatchId == id && item.TenantId == TenantId, cancellationToken);
        if (scope is null) return NotFound();
        db.LiveScoreScopes.Remove(scope);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/export.csv")]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var match = await db.Matches.AsNoTracking().Include(item => item.Participants).ThenInclude(item => item.Scores).SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken);
        if (match is null) return NotFound();
        var lines = new List<string> { "federation_number,full_name,total" };
        lines.AddRange(match.Participants.Select(participant => $"{Csv(participant.FederationNumber)},{Csv(participant.FullName)},{participant.Scores.Sum(item => item.Value)}"));
        return File(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", lines)), "text/csv", $"{match.ShortCode ?? match.Name}.csv");
    }

    private static string Csv(string? value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";
}