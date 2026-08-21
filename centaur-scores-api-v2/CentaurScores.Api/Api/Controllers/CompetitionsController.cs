using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Controllers;

[Route("api/competitions")]
public sealed class CompetitionsController(ApplicationDbContext db, ITenantContext tenantContext) : ApiControllerBase(tenantContext)
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
        var rule = new CompetitionScoreRule { Id = Guid.NewGuid(), TenantId = TenantId, CompetitionId = id, Name = request.Name, RoundIdsJson = System.Text.Json.JsonSerializer.Serialize(request.RoundIds), HighestScores = request.HighestScores, MinimumScores = request.MinimumScores, Aggregation = request.Aggregation };
        db.CompetitionScoreRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"api/competitions/{id}/scoring-rules/{rule.Id}", rule);
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
        var competition = await db.Competitions.AsNoTracking().Include(item => item.Rounds).ThenInclude(item => item.Matches).SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId, cancellationToken);
        if (competition is null) return NotFound();
        var matchIds = competition.Rounds.SelectMany(item => item.Matches).Select(item => item.MatchId).Distinct().ToList();
        var participants = await db.MatchParticipants.AsNoTracking().Include(item => item.Scores).Where(item => matchIds.Contains(item.MatchId) && item.TenantId == TenantId && item.ParticipantListMemberId != null).ToListAsync(cancellationToken);
        var results = participants.GroupBy(item => item.ParticipantListMemberId).Select(group => new { participantId = group.Key, name = group.First().FullName, total = group.Sum(item => item.Scores.Sum(score => score.Value)), matches = group.Count() }).OrderByDescending(item => item.total).ThenBy(item => item.name).ToList();
        return Ok(new { competition.Id, competition.Name, results });
    }
}