using CentaurScores.Api.Domain;

namespace CentaurScores.Api.Application;

public sealed record ParticipantResult(Guid ParticipantId, string Name, int Total, double Average, IReadOnlyDictionary<int, int> GroupScores);

public interface IScoringService
{
    ParticipantResult Calculate(MatchParticipant participant, int arrowsPerEnd, int? groupEnds);
    IReadOnlyList<ParticipantResult> Rank(IEnumerable<MatchParticipant> participants, Match match);
}

public sealed class ScoringService : IScoringService
{
    public ParticipantResult Calculate(MatchParticipant participant, int arrowsPerEnd, int? groupEnds)
    {
        var scores = participant.Scores.OrderBy(item => item.End).ThenBy(item => item.Arrow).ToList();
        var total = scores.Sum(item => item.Value);
        var groups = scores.GroupBy(item => groupEnds is > 0 ? (item.End - 1) / groupEnds.Value + 1 : item.End).ToDictionary(group => group.Key, group => group.Sum(item => item.Value));
        return new ParticipantResult(participant.Id, participant.FullName, total, scores.Count == 0 ? 0 : (double)total / scores.Count, groups);
    }

    public IReadOnlyList<ParticipantResult> Rank(IEnumerable<MatchParticipant> participants, Match match) => participants.Select(item => Calculate(item, match.ArrowsPerEnd, match.GroupEnds)).OrderByDescending(item => item.Total).ThenByDescending(item => item.GroupScores.Values.Sum()).ThenBy(item => item.Name).ToList();
}