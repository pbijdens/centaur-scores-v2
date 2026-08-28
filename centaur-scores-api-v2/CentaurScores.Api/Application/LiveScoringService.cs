using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using System.Text.Json;

namespace CentaurScores.Api.Application;

public interface ILiveScoringService
{
    IReadOnlyList<LiveScoringBlock> BuildBlocks(Match match, LiveScoreScope scope, IReadOnlyList<Category> categories, IReadOnlyDictionary<Guid, double>? personalBests = null);
}

public sealed class LiveScoringService(IScoringService scoringService) : ILiveScoringService
{
    public IReadOnlyList<LiveScoringBlock> BuildBlocks(Match match, LiveScoreScope scope, IReadOnlyList<Category> categories, IReadOnlyDictionary<Guid, double>? personalBests = null)
    {
        var categoryIds = Deserialize<List<Guid>>(scope.GroupByCategoryIdsJson) ?? [];
        var categoryOrder = Deserialize<KeyboardConfiguration>(match.KeyboardJson)?.CategoryOrder ?? [];
        var orderedCategoryIds = categoryOrder.Where(categoryIds.Contains).Concat(categoryIds.Where(id => !categoryOrder.Contains(id))).ToList();
        var categoryById = categories.ToDictionary(item => item.Id);
        var personalBestByParticipant = personalBests ?? new Dictionary<Guid, double>();

        return match.Participants
            .GroupBy(participant => GroupKey(participant, orderedCategoryIds))
            .Select(group => new LiveScoringBlock(
                GroupName(group.First(), orderedCategoryIds, categoryById),
                Rank(group, match, scope, personalBestByParticipant)))
            .Where(block => block.Entries.Count > 0)
            .OrderBy(block => block.Name)
            .ToList();
    }

    private IReadOnlyList<LiveScoringEntry> Rank(IEnumerable<MatchParticipant> participants, Match match, LiveScoreScope scope, IReadOnlyDictionary<Guid, double> personalBests)
    {
        var rows = participants.Select(participant => new RankedParticipant(participant, scoringService.Calculate(participant, match.ArrowsPerEnd, match.GroupEnds))).ToList();
        var rules = Deserialize<List<ScoringRule>>(match.ScoringRulesJson) ?? [];
        if (rules.Count == 0) rules.Add(new ScoringRule("total", null));

        var buckets = new List<List<RankedParticipant>> { rows };
        foreach (var rule in rules)
        {
            var nextBuckets = new List<List<RankedParticipant>>();
            foreach (var bucket in buckets)
            {
                if (bucket.Count <= 1)
                {
                    nextBuckets.Add(bucket);
                    continue;
                }

                foreach (var row in bucket)
                {
                    row.Values[rule] = RuleValue(row, rule);
                    if (rule.Type == "countKey" && !string.IsNullOrWhiteSpace(rule.KeyId)) row.UsedEqualizers.Add(rule.KeyId);
                }

                nextBuckets.AddRange(bucket.GroupBy(row => row.Values[rule]).OrderByDescending(group => group.Key).Select(group => group.ToList()));
            }
            buckets = nextBuckets;
        }

        var entries = new List<LiveScoringEntry>();
        var position = 1;
        foreach (var bucket in buckets)
        {
            var needsTieBreaker = bucket.Count > 1;
            foreach (var row in bucket.OrderBy(item => item.Result.Name))
            {
                entries.Add(CreateEntry(row, position, needsTieBreaker, scope, personalBests));
            }
            position += bucket.Count;
        }
        return entries;
    }

    private static LiveScoringEntry CreateEntry(RankedParticipant row, int position, bool needsTieBreaker, LiveScoreScope scope, IReadOnlyDictionary<Guid, double> personalBests)
    {
        var hasPersonalBest = personalBests.TryGetValue(row.Participant.Id, out var personalBest) && scope.IncludePersonalBest;

        var details = new List<string>();
        if (scope.IncludeGroupScores) details.Add(string.Join(", ", row.Result.GroupScores.OrderBy(item => item.Key).Select(item => item.Value)));
        if (scope.IncludeEqualizers && row.UsedEqualizers.Count > 0)
        {
            var equalizers = row.UsedEqualizers.Select(keyId => $"{row.Participant.Scores.Count(score => score.KeyId == keyId)}x{keyId}");
            details.Add($"({row.Result.Total} + {string.Join(", ", equalizers)})");
        }
        if (hasPersonalBest) details.Add($"Personal best: {personalBest:0.00}");

        return new LiveScoringEntry(
            position,
            needsTieBreaker,
            string.IsNullOrWhiteSpace(row.Participant.FullName) ? row.Participant.LastName : row.Participant.FullName,
            details.Count == 0 ? null : string.Join(" | ", details),
            scope.IncludeAverage ? Math.Round(row.Result.Average, 2) : null,
            row.Participant.Scores.Count,
            row.Result.Total,
            hasPersonalBest && row.Result.Average > personalBest);
    }

    private static int RuleValue(RankedParticipant row, ScoringRule rule) => rule.Type switch
    {
        "countKey" when !string.IsNullOrWhiteSpace(rule.KeyId) => row.Participant.Scores.Count(score => score.KeyId == rule.KeyId),
        _ => row.Result.Total
    };

    private static string GroupKey(MatchParticipant participant, IReadOnlyList<Guid> categoryIds) =>
        string.Join('|', categoryIds.Select(id => participant.Categories.GetValueOrDefault(id)));

    private static string GroupName(MatchParticipant participant, IReadOnlyList<Guid> categoryIds, IReadOnlyDictionary<Guid, Category> categories)
    {
        if (categoryIds.Count == 0) return "Results";
        return string.Join(", ", categoryIds.Select(id =>
        {
            var valueId = participant.Categories.GetValueOrDefault(id);
            return categories.GetValueOrDefault(id)?.Values.FirstOrDefault(value => value.ValueId == valueId)?.Name ?? valueId.ToString();
        }));
    }

    private static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { return default; }
    }

    private sealed record KeyboardConfiguration(List<Guid> CategoryOrder);
    private sealed record ScoringRule(string Type, string? KeyId);

    private sealed class RankedParticipant(MatchParticipant participant, ParticipantResult result)
    {
        public MatchParticipant Participant { get; } = participant;
        public ParticipantResult Result { get; } = result;
        public Dictionary<ScoringRule, int> Values { get; } = [];
        public List<string> UsedEqualizers { get; } = [];
    }
}