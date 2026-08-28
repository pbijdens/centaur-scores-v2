using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Controllers;

[Route("api/matches")]
public sealed class MatchesController(ApplicationDbContext db, ITenantContext tenantContext, IScoringService scoring, ILiveScoringService liveScoringService, IPersonalBestRegistrationService personalBestRegistrationService, IPersonalBestLiveLookup personalBestLiveLookup) : ApiControllerBase(tenantContext)
{
    // Lists never send the (potentially huge) Participants collection - callers get aggregate counts here and
    // fetch the full roster per-match via Get/Participants when they actually need it.
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.Matches.AsNoTracking()
        .Where(item => item.TenantId == TenantId)
        .OrderByDescending(item => item.IsOpen).ThenBy(item => item.Date)
        .Select(item => new MatchListItem(
            item.Id, item.TenantId, item.Name, item.Date, item.ShortCode, item.IsOpen, item.ParticipantListId,
            item.DeviceSelectionMode, item.Ends, item.ArrowsPerEnd, item.GroupEnds, item.AllowFreeParticipants,
            item.KeyboardJson, item.ScoringRulesJson,
            item.Participants.Count,
            item.Participants.Count(participant => participant.ParticipantListMemberId == null),
            item.LiveScopes))
        .ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CreateMatchRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var match = new Match { Id = Guid.NewGuid(), TenantId = TenantId, Name = request.Name, Date = request.Date, ShortCode = request.ShortCode, IsOpen = request.IsOpen, ParticipantListId = request.ParticipantListId, DeviceSelectionMode = string.IsNullOrWhiteSpace(request.DeviceSelectionMode) ? "list-and-free" : request.DeviceSelectionMode, Ends = request.Ends, ArrowsPerEnd = request.ArrowsPerEnd, GroupEnds = request.GroupEnds, AllowFreeParticipants = request.AllowFreeParticipants, KeyboardJson = string.IsNullOrWhiteSpace(request.KeyboardJson) ? MatchDefaults.KeyboardJson : request.KeyboardJson, ScoringRulesJson = request.ScoringRulesJson, PersonalBestClassifier = request.PersonalBestClassifier };
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
        var wasOpen = match.IsOpen;
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
        match.PersonalBestClassifier = request.PersonalBestClassifier;
        await db.SaveChangesAsync(cancellationToken);
        if (wasOpen && !match.IsOpen) await personalBestRegistrationService.RegisterOnDeactivationAsync(match, cancellationToken);
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
        // Loaded and saved per-match (rather than one ExecuteUpdateAsync) so RegisterOnDeactivationAsync
        // can run per match - ExecuteUpdateAsync bypasses change tracking entirely and would skip the hook.
        var openMatches = await db.Matches.Where(item => item.TenantId == TenantId && item.IsOpen).ToListAsync(cancellationToken);
        foreach (var match in openMatches) match.IsOpen = false;
        await db.SaveChangesAsync(cancellationToken);
        foreach (var match in openMatches) await personalBestRegistrationService.RegisterOnDeactivationAsync(match, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/participants")]
    public async Task<IActionResult> Participants(Guid id, CancellationToken cancellationToken) => Ok(await db.MatchParticipants.AsNoTracking().Include(item => item.Scores).Where(item => item.MatchId == id && item.TenantId == TenantId).OrderBy(item => item.DeviceId).ThenBy(item => item.DeviceOrder).ThenBy(item => item.LastName).ToListAsync(cancellationToken));

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
        personalBestLiveLookup.Invalidate(id);
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
        personalBestLiveLookup.Invalidate(id);
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
        personalBestLiveLookup.Invalidate(id);
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

    // Static equivalent of the public narrowcast page for a single, specific match/scope, regardless of IsOpen.
    [HttpGet("{id:guid}/live-scoring/{scope}")]
    public async Task<IActionResult> LiveScoringForScope(Guid id, string scope, CancellationToken cancellationToken)
    {
        var match = await db.Matches.AsNoTracking()
            .Include(item => item.Participants).ThenInclude(item => item.Scores)
            .Include(item => item.LiveScopes)
            .SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken);
        var liveScope = match?.LiveScopes.SingleOrDefault(item => item.Scope == scope);
        if (match is null || liveScope is null) return NotFound();

        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(item => item.Id == TenantId, cancellationToken);
        if (tenant is null) return NotFound();
        var categories = await db.Categories.AsNoTracking().Include(item => item.Values).Where(item => item.TenantId == TenantId).ToListAsync(cancellationToken);
        var personalBests = await personalBestLiveLookup.BuildAsync(match, liveScope, cancellationToken);
        return Ok(new LiveScoringPage(15, tenant.LogoUrl, tenant.Name, match.Name, match.Date, match.Ends, match.ArrowsPerEnd, match.GroupEnds, liveScoringService.BuildBlocks(match, liveScope, categories, personalBests)));
    }

    [HttpPost("{id:guid}/devices")]
    public async Task<IActionResult> AddDevice(Guid id, CreateDeviceRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!await db.Matches.AnyAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken)) return NotFound();
        var nextOrder = (await db.ScoreDevices.Where(item => item.MatchId == id && item.TenantId == TenantId).MaxAsync(item => (int?)item.SortOrder, cancellationToken) ?? -1) + 1;
        var device = new ScoreDevice { Id = Guid.NewGuid(), TenantId = TenantId, MatchId = id, Name = request.Name, SortOrder = nextOrder };
        db.ScoreDevices.Add(device);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"api/matches/{id}/devices/{device.Id}", device);
    }

    [HttpPut("{id:guid}/devices/order")]
    public async Task<IActionResult> ReorderDevices(Guid id, ReorderDevicesRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var devices = await db.ScoreDevices.Where(item => item.MatchId == id && item.TenantId == TenantId).OrderBy(item => item.SortOrder).ToListAsync(cancellationToken);
        if (request.DeviceIds.Count != devices.Count) return BadRequest(new { message = "All devices must be included." });
        if (request.DeviceIds.Distinct().Count() != request.DeviceIds.Count) return BadRequest(new { message = "Duplicate device IDs are not allowed." });
        var byId = devices.ToDictionary(item => item.Id, item => item);
        for (var index = 0; index < request.DeviceIds.Count; index++)
        {
            if (!byId.TryGetValue(request.DeviceIds[index], out var device)) return BadRequest(new { message = "Unknown device ID in reorder request." });
            device.SortOrder = index;
        }
        await db.SaveChangesAsync(cancellationToken);
        return Ok(devices.OrderBy(item => item.SortOrder));
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
        if (request.DeviceId is null)
        {
            participant.DeviceId = null;
            participant.DeviceOrder = null;
            await db.SaveChangesAsync(cancellationToken);
            return Ok(participant);
        }

        var isSameDevice = participant.DeviceId == request.DeviceId;
        participant.DeviceId = request.DeviceId;
        if (!isSameDevice)
        {
            var nextOrder = (await db.MatchParticipants
                .Where(item => item.MatchId == id && item.TenantId == TenantId && item.DeviceId == request.DeviceId)
                .MaxAsync(item => (int?)item.DeviceOrder, cancellationToken) ?? -1) + 1;
            participant.DeviceOrder = nextOrder;
        }
        else if (participant.DeviceOrder is null)
        {
            participant.DeviceOrder = 0;
        }
        await db.SaveChangesAsync(cancellationToken);
        return Ok(participant);
    }

    [HttpPut("{id:guid}/devices/{deviceId:guid}/participants/order")]
    public async Task<IActionResult> ReorderDeviceParticipants(Guid id, Guid deviceId, ReorderDeviceParticipantsRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!await db.ScoreDevices.AnyAsync(item => item.Id == deviceId && item.MatchId == id && item.TenantId == TenantId, cancellationToken)) return NotFound();

        var assignedParticipants = await db.MatchParticipants
            .Where(item => item.MatchId == id && item.TenantId == TenantId && item.DeviceId == deviceId)
            .OrderBy(item => item.DeviceOrder)
            .ThenBy(item => item.LastName)
            .ToListAsync(cancellationToken);

        if (request.ParticipantIds.Count != assignedParticipants.Count) return BadRequest(new { message = "All assigned participants must be included." });
        if (request.ParticipantIds.Distinct().Count() != request.ParticipantIds.Count) return BadRequest(new { message = "Duplicate participant IDs are not allowed." });

        var byId = assignedParticipants.ToDictionary(item => item.Id, item => item);
        for (var index = 0; index < request.ParticipantIds.Count; index++)
        {
            if (!byId.TryGetValue(request.ParticipantIds[index], out var participant)) return BadRequest(new { message = "Unknown participant ID in reorder request." });
            participant.DeviceOrder = index;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(assignedParticipants.OrderBy(item => item.DeviceOrder));
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

        var keyboard = ParseKeyboardConfiguration(match.KeyboardJson);
        var categoryIds = keyboard.CategoryOrder.Distinct().ToList();
        var categoriesById = await db.Categories.AsNoTracking()
            .Include(item => item.Values)
            .Where(item => item.TenantId == TenantId && categoryIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var categories = categoryIds.Where(categoriesById.ContainsKey).Select(id => categoriesById[id]).ToList();
        var splitSize = match.GroupEnds is > 0 ? match.GroupEnds.Value : 1;
        var splitCount = match.Ends > 0 ? (match.Ends + splitSize - 1) / splitSize : 0;

        var headers = new List<string> { "federation_number", "full_name", "total" };
        headers.AddRange(keyboard.Keyboard.Select(key => Csv(key.Label)));
        headers.Add("Null");
        headers.AddRange(Enumerable.Range(1, splitCount).Select(index => $"Split{index}"));
        headers.AddRange(categories.Select(category => Csv(category.Name)));
        headers.Add("lastname");

        var lines = new List<string> { string.Join(",", headers) };
        foreach (var participant in match.Participants)
        {
            var result = scoring.Calculate(participant, match.ArrowsPerEnd, match.GroupEnds);
            var values = new List<string> { Csv(participant.FederationNumber), Csv(participant.FullName), result.Total.ToString() };
            values.AddRange(keyboard.Keyboard.Select(key => participant.Scores.Count(score => score.KeyId == key.KeyId).ToString()));
            values.Add(Math.Max(match.Ends * match.ArrowsPerEnd - participant.Scores.Count, 0).ToString());
            values.AddRange(Enumerable.Range(1, splitCount).Select(index => result.GroupScores.GetValueOrDefault(index).ToString()));
            values.AddRange(categories.Select(category => Csv(CategoryValueName(participant, category))));
            values.Add(Csv(participant.LastName));
            lines.Add(string.Join(",", values));
        }

        return File(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", lines)), "text/csv", $"{match.ShortCode ?? match.Name}.csv");
    }

    private static KeyboardConfiguration ParseKeyboardConfiguration(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<KeyboardConfiguration>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new([], []);
        }
        catch (System.Text.Json.JsonException)
        {
            return new([], []);
        }
    }

    private static string CategoryValueName(MatchParticipant participant, Category category)
    {
        if (!participant.Categories.TryGetValue(category.Id, out var valueId)) return "";
        return category.Values.FirstOrDefault(value => value.ValueId == valueId)?.Name ?? valueId.ToString();
    }

    private static string Csv(string? value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";

    private sealed record KeyboardConfiguration(List<Guid> CategoryOrder, List<KeyboardKey> Keyboard);
    private sealed record KeyboardKey(string KeyId, string Label);
}