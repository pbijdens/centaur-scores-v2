using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Controllers;

[Route("api/competitions")]
public sealed class CompetitionsController(ApplicationDbContext db, ITenantContext tenantContext, ICompetitionService competitionService) : ApiControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.Competitions.AsNoTracking().Include(item => item.Rounds).Include(item => item.ScoringRules).Where(item => item.TenantId == TenantId).OrderBy(item => item.StartDate).ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CreateCompetitionRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var competition = new Competition { Id = Guid.NewGuid(), TenantId = TenantId, Name = request.Name, StartDate = request.StartDate, EndDate = request.EndDate, GroupByCategoryIdsJson = System.Text.Json.JsonSerializer.Serialize(request.GroupByCategoryIds) };
        db.Competitions.Add(competition);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = competition.Id }, competition);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) => await db.Competitions.AsNoTracking().Include(item => item.Rounds).ThenInclude(item => item.Matches).Include(item => item.ScoringRules).SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken) is { } competition ? Ok(competition) : NotFound();

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CreateCompetitionRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var competition = await db.Competitions.SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken);
        if (competition is null) return NotFound();
        competition.Name = request.Name;
        competition.StartDate = request.StartDate;
        competition.EndDate = request.EndDate;
        competition.GroupByCategoryIdsJson = System.Text.Json.JsonSerializer.Serialize(request.GroupByCategoryIds);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(competition);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        var competition = await db.Competitions.SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken);
        if (competition is null) return NotFound();
        db.Competitions.Remove(competition);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/rounds")]
    public async Task<IActionResult> AddRound(Guid id, CreateRoundRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!await db.Competitions.AnyAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken)) return NotFound();
        var round = new CompetitionRound { Id = Guid.NewGuid(), TenantId = TenantId, CompetitionId = id, Order = request.Order, ShortName = request.ShortName, LongName = request.LongName };
        db.CompetitionRounds.Add(round);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"api/competitions/{id}/rounds/{round.Id}", round);
    }

    [HttpDelete("{id:guid}/rounds/{roundId:guid}")]
    public async Task<IActionResult> DeleteRound(Guid id, Guid roundId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var round = await db.CompetitionRounds.SingleOrDefaultAsync(item => item.Id == roundId && item.CompetitionId == id && item.TenantId == TenantId, cancellationToken);
        if (round is null) return NotFound();
        db.CompetitionRounds.Remove(round);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/rounds/{roundId:guid}")]
    public async Task<IActionResult> UpdateRound(Guid id, Guid roundId, UpdateRoundRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var round = await db.CompetitionRounds.SingleOrDefaultAsync(item => item.Id == roundId && item.CompetitionId == id && item.TenantId == TenantId, cancellationToken);
        if (round is null) return NotFound();
        round.ShortName = request.ShortName;
        round.LongName = request.LongName;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(round);
    }

    [HttpPut("{id:guid}/rounds/order")]
    public async Task<IActionResult> ReorderRounds(Guid id, ReorderRoundsRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var rounds = await db.CompetitionRounds.Where(item => item.CompetitionId == id && item.TenantId == TenantId).OrderBy(item => item.Order).ToListAsync(cancellationToken);
        if (request.RoundIds.Count != rounds.Count) return BadRequest(new { message = "All rounds must be included." });
        if (request.RoundIds.Distinct().Count() != request.RoundIds.Count) return BadRequest(new { message = "Duplicate round IDs are not allowed." });
        var byId = rounds.ToDictionary(item => item.Id, item => item);
        for (var index = 0; index < request.RoundIds.Count; index++)
        {
            if (!byId.TryGetValue(request.RoundIds[index], out var round)) return BadRequest(new { message = "Unknown round ID in reorder request." });
            round.Order = index;
        }
        await db.SaveChangesAsync(cancellationToken);
        return Ok(rounds.OrderBy(item => item.Order));
    }

    [HttpPost("{id:guid}/rounds/{roundId:guid}/matches")]
    public async Task<IActionResult> AssignMatch(Guid id, Guid roundId, AssignMatchRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!await db.CompetitionRounds.AnyAsync(item => item.Id == roundId && item.CompetitionId == id && item.TenantId == TenantId, cancellationToken) || !await db.Matches.AnyAsync(item => item.Id == request.MatchId && item.TenantId == TenantId, cancellationToken)) return NotFound();
        var assignment = new CompetitionRoundMatch { Id = Guid.NewGuid(), TenantId = TenantId, CompetitionRoundId = roundId, MatchId = request.MatchId };
        db.CompetitionRoundMatches.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(assignment);
    }

    [HttpDelete("{id:guid}/rounds/{roundId:guid}/matches/{matchId:guid}")]
    public async Task<IActionResult> UnassignMatch(Guid id, Guid roundId, Guid matchId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var assignment = await db.CompetitionRoundMatches.SingleOrDefaultAsync(item => item.CompetitionRoundId == roundId && item.MatchId == matchId && item.TenantId == TenantId, cancellationToken);
        if (assignment is null) return NotFound();
        db.CompetitionRoundMatches.Remove(assignment);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/scoring-rules")]
    public async Task<IActionResult> AddRule(Guid id, CreateCompetitionRuleRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        if (!await db.Competitions.AnyAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken)) return NotFound();
        var existingSortOrders = await db.CompetitionScoreRules.Where(item => item.CompetitionId == id && item.TenantId == TenantId).Select(item => item.SortOrder).ToListAsync(cancellationToken);
        var sortOrder = existingSortOrders.Count == 0 ? 0 : existingSortOrders.Max() + 1;
        var rule = new CompetitionScoreRule { Id = Guid.NewGuid(), TenantId = TenantId, CompetitionId = id, Name = request.Name, RoundIdsJson = System.Text.Json.JsonSerializer.Serialize(request.RoundIds), HighestScores = request.HighestScores, MinimumScores = request.MinimumScores, Aggregation = request.Aggregation, SortOrder = sortOrder };
        db.CompetitionScoreRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"api/competitions/{id}/scoring-rules/{rule.Id}", rule);
    }

    [HttpPut("{id:guid}/scoring-rules/{ruleId:guid}")]
    public async Task<IActionResult> UpdateRule(Guid id, Guid ruleId, UpdateCompetitionRuleRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var rule = await db.CompetitionScoreRules.SingleOrDefaultAsync(item => item.Id == ruleId && item.CompetitionId == id && item.TenantId == TenantId, cancellationToken);
        if (rule is null) return NotFound();
        rule.Name = request.Name;
        rule.RoundIdsJson = System.Text.Json.JsonSerializer.Serialize(request.RoundIds);
        rule.HighestScores = request.HighestScores;
        rule.MinimumScores = request.MinimumScores;
        rule.Aggregation = request.Aggregation;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(rule);
    }

    [HttpPut("{id:guid}/scoring-rules/order")]
    public async Task<IActionResult> ReorderRules(Guid id, ReorderCompetitionRulesRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var rules = await db.CompetitionScoreRules.Where(item => item.CompetitionId == id && item.TenantId == TenantId).OrderBy(item => item.SortOrder).ToListAsync(cancellationToken);
        if (request.RuleIds.Count != rules.Count) return BadRequest(new { message = "All scoring rules must be included." });
        if (request.RuleIds.Distinct().Count() != request.RuleIds.Count) return BadRequest(new { message = "Duplicate scoring rule IDs are not allowed." });
        var byId = rules.ToDictionary(item => item.Id, item => item);
        for (var index = 0; index < request.RuleIds.Count; index++)
        {
            if (!byId.TryGetValue(request.RuleIds[index], out var rule)) return BadRequest(new { message = "Unknown scoring rule ID in reorder request." });
            rule.SortOrder = index;
        }
        await db.SaveChangesAsync(cancellationToken);
        return Ok(rules.OrderBy(item => item.SortOrder));
    }

    [HttpDelete("{id:guid}/scoring-rules/{ruleId:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id, Guid ruleId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var rule = await db.CompetitionScoreRules.SingleOrDefaultAsync(item => item.Id == ruleId && item.CompetitionId == id && item.TenantId == TenantId, cancellationToken);
        if (rule is null) return NotFound();
        db.CompetitionScoreRules.Remove(rule);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/results")]
    public async Task<IActionResult> Results(Guid id, CancellationToken cancellationToken)
    {
        var competition = await db.Competitions.AsNoTracking().Include(item => item.Rounds).ThenInclude(item => item.Matches).Include(item => item.ScoringRules).SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken);
        if (competition is null) return NotFound();
        var categories = await db.Categories.AsNoTracking().Include(item => item.Values).Where(item => item.TenantId == TenantId).ToListAsync(cancellationToken);
        var matchIds = competition.Rounds.SelectMany(item => item.Matches).Select(item => item.MatchId).Distinct().ToList();
        var matches = await db.Matches.AsNoTracking().Include(item => item.Participants).ThenInclude(item => item.Scores).Where(item => matchIds.Contains(item.Id) && item.TenantId == TenantId).ToListAsync(cancellationToken);
        var matchesById = matches.ToDictionary(item => item.Id);
        var matchesByRound = competition.Rounds.ToDictionary(
            round => round.Id,
            round => (IReadOnlyList<Match>)round.Matches.Select(item => matchesById.GetValueOrDefault(item.MatchId)).Where(item => item is not null).Cast<Match>().ToList());
        return Ok(competitionService.BuildResults(competition, categories, matchesByRound));
    }
}