using CentaurScores.Api.Domain;

namespace CentaurScores.Api.Application;

public sealed record CompetitionParticipantResult(Guid ParticipantId, string Name, int Total, bool Disqualified, IReadOnlyDictionary<string, int> RuleScores);

public interface ICompetitionService
{
    IReadOnlyList<CompetitionParticipantResult> Calculate(Competition competition, IReadOnlyDictionary<Guid, IReadOnlyList<ParticipantResult>> resultsByRound);
}

public sealed class CompetitionService : ICompetitionService
{
    public IReadOnlyList<CompetitionParticipantResult> Calculate(Competition competition, IReadOnlyDictionary<Guid, IReadOnlyList<ParticipantResult>> resultsByRound)
    {
        var participants = resultsByRound.Values.SelectMany(value => value).GroupBy(item => item.ParticipantId);
        return participants.Select(participant =>
        {
            var ruleScores = new Dictionary<string, int>();
            var disqualified = false;
            foreach (var rule in competition.ScoringRules.OrderBy(item => item.Id))
            {
                var scores = resultsByRound.Where(pair => JsonIds(rule.RoundIdsJson).Contains(pair.Key)).SelectMany(pair => pair.Value.Where(item => item.ParticipantId == participant.Key)).Select(item => rule.Aggregation == "average" ? (int)Math.Round(item.Average) : item.Total).OrderByDescending(item => item).ToList();
                if (scores.Count < rule.MinimumScores) disqualified = true;
                ruleScores[rule.Name] = scores.Take(rule.HighestScores).Sum();
            }
            return new CompetitionParticipantResult(participant.Key, participant.First().Name, disqualified ? 0 : ruleScores.Values.Sum(), disqualified, ruleScores);
        }).OrderByDescending(item => item.Disqualified ? -1 : item.Total).ThenBy(item => item.Name).ToList();
    }

    private static IReadOnlySet<Guid> JsonIds(string json) => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(json) is { } ids ? ids.ToHashSet() : new HashSet<Guid>();
}