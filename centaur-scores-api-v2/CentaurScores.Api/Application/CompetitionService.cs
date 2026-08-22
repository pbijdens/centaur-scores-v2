using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using System.Text.Json;

namespace CentaurScores.Api.Application;

public sealed record CompetitionParticipantResult(Guid ParticipantId, string Name, int Total, bool Disqualified, IReadOnlyDictionary<string, int> RuleScores, IReadOnlyDictionary<string, IReadOnlySet<Guid>>? UsedRoundIdsByRule = null);

public interface ICompetitionService
{
    IReadOnlyList<CompetitionParticipantResult> Calculate(Competition competition, IReadOnlyDictionary<Guid, IReadOnlyList<ParticipantResult>> totalResultsByRound, IReadOnlyDictionary<Guid, IReadOnlyList<ParticipantResult>>? f1ResultsByRound = null);
    CompetitionResultsDocument BuildResults(Competition competition, IReadOnlyList<Category> categories, IReadOnlyDictionary<Guid, IReadOnlyList<Match>> matchesByRound);
}

public sealed class CompetitionService(IScoringService scoringService) : ICompetitionService
{
    // Position -> f1-style points for #1..#9; every lower position with a non-zero score earns 1 point.
    private static readonly int[] F1PointsTable = [12, 10, 8, 7, 6, 5, 4, 3, 2];

    // Tie-break precedence for the final competition standings: count of X, then 10, then 9, ... down to 1.
    private static readonly string[] TieBreakKeyOrder = ["X", "10", "9", "8", "7", "6", "5", "4", "3", "2", "1"];

    public IReadOnlyList<CompetitionParticipantResult> Calculate(Competition competition, IReadOnlyDictionary<Guid, IReadOnlyList<ParticipantResult>> totalResultsByRound, IReadOnlyDictionary<Guid, IReadOnlyList<ParticipantResult>>? f1ResultsByRound = null)
    {
        var namesByParticipant = totalResultsByRound.Values.SelectMany(value => value).GroupBy(item => item.ParticipantId).ToDictionary(group => group.Key, group => group.First().Name);

        return namesByParticipant.Keys.Select(participantId =>
        {
            var ruleScores = new Dictionary<string, int>();
            var usedRoundIdsByRule = new Dictionary<string, IReadOnlySet<Guid>>();
            var disqualified = false;
            foreach (var rule in competition.ScoringRules.OrderBy(item => item.SortOrder))
            {
                var roundIds = JsonIds(rule.RoundIdsJson);
                var source = rule.Aggregation == "f1" && f1ResultsByRound is not null ? f1ResultsByRound : totalResultsByRound;
                var perRound = source
                    .Where(pair => roundIds.Contains(pair.Key))
                    .SelectMany(pair => pair.Value.Where(item => item.ParticipantId == participantId).Select(item => (RoundId: pair.Key, item.Total)))
                    .OrderByDescending(item => item.Total)
                    .ToList();
                if (perRound.Count < rule.MinimumScores) disqualified = true;
                var used = perRound.Take(rule.HighestScores).ToList();
                ruleScores[rule.Name] = used.Sum(item => item.Total);
                usedRoundIdsByRule[rule.Name] = used.Select(item => item.RoundId).ToHashSet();
            }
            return new CompetitionParticipantResult(participantId, namesByParticipant[participantId], disqualified ? 0 : ruleScores.Values.Sum(), disqualified, ruleScores, usedRoundIdsByRule);
        }).OrderByDescending(item => item.Disqualified ? -1 : item.Total).ThenBy(item => item.Name).ToList();
    }

    public CompetitionResultsDocument BuildResults(Competition competition, IReadOnlyList<Category> categories, IReadOnlyDictionary<Guid, IReadOnlyList<Match>> matchesByRound)
    {
        var groupCategoryIds = JsonIds(competition.GroupByCategoryIdsJson).ToList();
        var totalResultsByRound = matchesByRound.ToDictionary(pair => pair.Key, pair => BuildRoundTotals(pair.Value));
        var f1ResultsByRound = matchesByRound.ToDictionary(pair => pair.Key, pair => BuildRoundF1Points(pair.Value, groupCategoryIds));
        var ruleResults = Calculate(
            competition,
            totalResultsByRound.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<ParticipantResult>)pair.Value),
            f1ResultsByRound.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<ParticipantResult>)pair.Value));

        var categoriesByParticipant = new Dictionary<Guid, IReadOnlyDictionary<Guid, int>>();
        var keyCountsByParticipant = new Dictionary<Guid, Dictionary<string, int>>();
        foreach (var match in matchesByRound.Values.SelectMany(item => item).DistinctBy(item => item.Id))
        {
            foreach (var participant in match.Participants.Where(item => item.ParticipantListMemberId is not null))
            {
                var participantId = participant.ParticipantListMemberId!.Value;
                categoriesByParticipant.TryAdd(participantId, participant.Categories);
                if (!keyCountsByParticipant.TryGetValue(participantId, out var counts)) { counts = []; keyCountsByParticipant[participantId] = counts; }
                foreach (var score in participant.Scores) counts[score.KeyId] = counts.GetValueOrDefault(score.KeyId) + 1;
            }
        }

        var categoryById = categories.ToDictionary(item => item.Id);
        var rounds = competition.Rounds.OrderBy(item => item.Order).Select(item => new CompetitionResultRound(item.Id, item.ShortName, item.LongName, item.Order)).ToList();

        var groups = ruleResults
            .GroupBy(item => GroupNameFor(categoriesByParticipant.GetValueOrDefault(item.ParticipantId, new Dictionary<Guid, int>()), groupCategoryIds, categoryById))
            .OrderBy(group => group.Key)
            .Select(group => new CompetitionResultGroup(group.Key, BuildGroupEntries(group.ToList(), totalResultsByRound, keyCountsByParticipant, rounds)))
            .ToList();

        return new CompetitionResultsDocument(competition.Name, competition.StartDate, competition.EndDate, rounds, groups);
    }

    private IReadOnlyList<ParticipantResult> BuildRoundTotals(IReadOnlyList<Match> matches)
    {
        return matches
            .SelectMany(match => match.Participants.Where(item => item.ParticipantListMemberId is not null).Select(item => (Match: match, Participant: item)))
            .GroupBy(item => item.Participant.ParticipantListMemberId!.Value)
            .Select(group =>
            {
                var totalScore = group.Sum(item => scoringService.Calculate(item.Participant, item.Match.ArrowsPerEnd, item.Match.GroupEnds).Total);
                var totalArrows = group.Sum(item => item.Participant.Scores.Count);
                var name = group.First().Participant.FullName;
                var displayName = string.IsNullOrWhiteSpace(name) ? group.First().Participant.LastName : name;
                return new ParticipantResult(group.Key, displayName, totalScore, totalArrows == 0 ? 0 : (double)totalScore / totalArrows, new Dictionary<int, int>());
            }).ToList();
    }

    private IReadOnlyList<ParticipantResult> BuildRoundF1Points(IReadOnlyList<Match> matches, IReadOnlyList<Guid> groupCategoryIds)
    {
        var totals = new Dictionary<Guid, int>();
        var names = new Dictionary<Guid, string>();
        foreach (var match in matches)
        {
            foreach (var (participantId, points) in ComputeMatchF1Points(match, groupCategoryIds))
            {
                totals[participantId] = totals.GetValueOrDefault(participantId) + points;
            }
            foreach (var participant in match.Participants.Where(item => item.ParticipantListMemberId is not null))
            {
                var displayName = string.IsNullOrWhiteSpace(participant.FullName) ? participant.LastName : participant.FullName;
                names[participant.ParticipantListMemberId!.Value] = displayName;
            }
        }
        return totals.Select(pair => new ParticipantResult(pair.Key, names.GetValueOrDefault(pair.Key, ""), pair.Value, 0, new Dictionary<int, int>())).ToList();
    }

    private Dictionary<Guid, int> ComputeMatchF1Points(Match match, IReadOnlyList<Guid> groupCategoryIds)
    {
        var rules = Deserialize<List<ScoringRuleDef>>(match.ScoringRulesJson) ?? [];
        if (rules.Count == 0) rules.Add(new ScoringRuleDef("total", null));

        var points = new Dictionary<Guid, int>();
        var eligible = match.Participants.Where(item => item.ParticipantListMemberId is not null).ToList();
        foreach (var group in eligible.GroupBy(item => GroupKey(item, groupCategoryIds)))
        {
            var rows = group.Select(item => new RankedRow(item, scoringService.Calculate(item, match.ArrowsPerEnd, match.GroupEnds))).ToList();
            var buckets = new List<List<RankedRow>> { rows };
            foreach (var rule in rules)
            {
                var next = new List<List<RankedRow>>();
                foreach (var bucket in buckets)
                {
                    if (bucket.Count <= 1) { next.Add(bucket); continue; }
                    next.AddRange(bucket.GroupBy(row => RuleValue(row, rule)).OrderByDescending(item => item.Key).Select(item => item.ToList()));
                }
                buckets = next;
            }

            var position = 1;
            foreach (var bucket in buckets)
            {
                var pointsForPosition = position <= F1PointsTable.Length ? F1PointsTable[position - 1] : 1;
                foreach (var row in bucket)
                {
                    var value = row.Result.Total > 0 ? pointsForPosition : 0;
                    var participantId = row.Participant.ParticipantListMemberId!.Value;
                    points[participantId] = points.GetValueOrDefault(participantId) + value;
                }
                position += bucket.Count;
            }
        }
        return points;
    }

    private static IReadOnlyList<CompetitionResultEntry> BuildGroupEntries(IReadOnlyList<CompetitionParticipantResult> participants, IReadOnlyDictionary<Guid, IReadOnlyList<ParticipantResult>> totalResultsByRound, IReadOnlyDictionary<Guid, Dictionary<string, int>> keyCountsByParticipant, IReadOnlyList<CompetitionResultRound> rounds)
    {
        var qualifying = participants.Where(item => !item.Disqualified)
            .OrderByDescending(item => item.Total)
            .ThenByDescending(item => TieBreakKey(item.ParticipantId, keyCountsByParticipant), TieBreakComparer.Instance)
            .ToList();

        var entries = new List<CompetitionResultEntry>();
        var position = 1;
        var index = 0;
        while (index < qualifying.Count)
        {
            var leader = qualifying[index];
            var leaderTieBreak = TieBreakKey(leader.ParticipantId, keyCountsByParticipant);
            var bucket = qualifying.Skip(index).TakeWhile(item => item.Total == leader.Total && TieBreakKey(item.ParticipantId, keyCountsByParticipant).SequenceEqual(leaderTieBreak)).ToList();
            var needsTieBreaker = bucket.Count > 1;
            foreach (var participant in bucket)
            {
                entries.Add(BuildEntry(participant, position.ToString(), needsTieBreaker, totalResultsByRound, rounds));
            }
            position += bucket.Count;
            index += bucket.Count;
        }

        foreach (var participant in participants.Where(item => item.Disqualified).OrderBy(item => item.Name))
        {
            entries.Add(BuildEntry(participant, "-", false, totalResultsByRound, rounds));
        }

        return entries;
    }

    private static CompetitionResultEntry BuildEntry(CompetitionParticipantResult participant, string position, bool needsTieBreaker, IReadOnlyDictionary<Guid, IReadOnlyList<ParticipantResult>> totalResultsByRound, IReadOnlyList<CompetitionResultRound> rounds)
    {
        var roundScores = new Dictionary<Guid, CompetitionResultScore>();
        foreach (var round in rounds)
        {
            var entry = totalResultsByRound.GetValueOrDefault(round.Id)?.FirstOrDefault(item => item.ParticipantId == participant.ParticipantId);
            if (entry is null) continue;
            var used = participant.UsedRoundIdsByRule is null || participant.UsedRoundIdsByRule.Values.Any(set => set.Contains(round.Id));
            roundScores[round.Id] = new CompetitionResultScore(entry.Total, used);
        }
        return new CompetitionResultEntry(participant.Disqualified ? "-" : position, needsTieBreaker, participant.Name, participant.Disqualified, participant.Disqualified ? null : participant.Total, roundScores, participant.RuleScores);
    }

    private static int[] TieBreakKey(Guid participantId, IReadOnlyDictionary<Guid, Dictionary<string, int>> keyCountsByParticipant)
    {
        var counts = keyCountsByParticipant.GetValueOrDefault(participantId);
        return TieBreakKeyOrder.Select(key => counts?.GetValueOrDefault(key) ?? 0).ToArray();
    }

    private static int RuleValue(RankedRow row, ScoringRuleDef rule) => rule.Type switch
    {
        "countKey" when !string.IsNullOrWhiteSpace(rule.KeyId) => row.Participant.Scores.Count(score => score.KeyId == rule.KeyId),
        _ => row.Result.Total
    };

    private static string GroupKey(MatchParticipant participant, IReadOnlyList<Guid> categoryIds) =>
        string.Join('|', categoryIds.Select(id => participant.Categories.GetValueOrDefault(id)));

    private static string GroupNameFor(IReadOnlyDictionary<Guid, int> participantCategories, IReadOnlyList<Guid> categoryIds, IReadOnlyDictionary<Guid, Category> categoryById)
    {
        if (categoryIds.Count == 0) return "Results";
        return string.Join(", ", categoryIds.Select(id =>
        {
            var valueId = participantCategories.GetValueOrDefault(id);
            return categoryById.GetValueOrDefault(id)?.Values.FirstOrDefault(value => value.ValueId == valueId)?.Name ?? valueId.ToString();
        }));
    }

    private static IReadOnlySet<Guid> JsonIds(string json) => Deserialize<List<Guid>>(json) is { } ids ? ids.ToHashSet() : new HashSet<Guid>();

    private static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { return default; }
    }

    private sealed record ScoringRuleDef(string Type, string? KeyId);

    private sealed class RankedRow(MatchParticipant participant, ParticipantResult result)
    {
        public MatchParticipant Participant { get; } = participant;
        public ParticipantResult Result { get; } = result;
    }

    private sealed class TieBreakComparer : IComparer<int[]>
    {
        public static readonly TieBreakComparer Instance = new();

        public int Compare(int[]? x, int[]? y)
        {
            if (x is null || y is null) return 0;
            for (var i = 0; i < x.Length && i < y.Length; i++)
            {
                var comparison = x[i].CompareTo(y[i]);
                if (comparison != 0) return comparison;
            }
            return 0;
        }
    }
}