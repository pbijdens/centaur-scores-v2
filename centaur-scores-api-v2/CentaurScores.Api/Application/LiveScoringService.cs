using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using System.Text.Json;

namespace CentaurScores.Api.Application;

public interface ILiveScoringService
{
    IReadOnlyList<LiveScoringBlock> BuildBlocks(Match match, LiveScoreScope scope, IReadOnlyList<Category> categories, IReadOnlyDictionary<Guid, PersonalBestSnapshot>? personalBests = null);

    /// <summary>
    /// The anonymous published-results equivalent of BuildBlocks: same grouping, ordering and ranking (match scoring rules included),
    /// but plain result rows instead of narrowcast lines. An empty groupByCategoryIds yields a single block with every participant.
    /// </summary>
    IReadOnlyList<PublishedMatchBlock> BuildPublishedBlocks(Match match, IReadOnlyList<Guid> groupByCategoryIds, IReadOnlyList<Category> categories);

    /// <summary>The scope's group-by categories in the match's category order - what BuildBlocks groups on.</summary>
    IReadOnlyList<Guid> OrderedGroupCategoryIds(Match match, IReadOnlyList<Guid> groupByCategoryIds);
}

public sealed class LiveScoringService(IScoringService scoringService) : ILiveScoringService
{
    public IReadOnlyList<LiveScoringBlock> BuildBlocks(Match match, LiveScoreScope scope, IReadOnlyList<Category> categories, IReadOnlyDictionary<Guid, PersonalBestSnapshot>? personalBests = null)
    {
        var orderedCategoryIds = OrderedGroupCategoryIds(match, Deserialize<List<Guid>>(scope.GroupByCategoryIdsJson) ?? []);
        var categoryById = categories.ToDictionary(item => item.Id);
        var personalBestByParticipant = personalBests ?? new Dictionary<Guid, PersonalBestSnapshot>();

        return match.Participants
            .GroupBy(participant => GroupKey(participant, orderedCategoryIds))
            .Select(group => new LiveScoringBlock(
                GroupName(group.First(), orderedCategoryIds, categoryById),
                Rank(group, match, scope, personalBestByParticipant, categoryById)))
            .Where(block => block.Entries.Count > 0)
            .OrderBy(block => block.Name)
            .ToList();
    }

    public IReadOnlyList<Guid> OrderedGroupCategoryIds(Match match, IReadOnlyList<Guid> groupByCategoryIds)
    {
        var categoryOrder = Deserialize<KeyboardConfiguration>(match.KeyboardJson)?.CategoryOrder ?? [];
        return categoryOrder.Where(groupByCategoryIds.Contains).Concat(groupByCategoryIds.Where(id => !categoryOrder.Contains(id))).Distinct().ToList();
    }

    public IReadOnlyList<PublishedMatchBlock> BuildPublishedBlocks(Match match, IReadOnlyList<Guid> groupByCategoryIds, IReadOnlyList<Category> categories)
    {
        var orderedCategoryIds = OrderedGroupCategoryIds(match, groupByCategoryIds);
        var categoryById = categories.ToDictionary(item => item.Id);
        return match.Participants
            .GroupBy(participant => GroupKey(participant, orderedCategoryIds))
            .Select(group => new PublishedMatchBlock(
                GroupName(group.First(), orderedCategoryIds, categoryById),
                RankRows(group, match).Select(item => new PublishedMatchEntry(
                    item.Position,
                    item.NeedsTieBreaker,
                    DisplayName(item.Row.Participant),
                    item.Row.Participant.FederationNumber,
                    Math.Round(item.Row.Result.Average, 2),
                    item.Row.Participant.Scores.Count,
                    item.Row.Result.Total)).ToList()))
            .Where(block => block.Entries.Count > 0)
            .OrderBy(block => block.Name)
            .ToList();
    }

    private IReadOnlyList<LiveScoringEntry> Rank(IEnumerable<MatchParticipant> participants, Match match, LiveScoreScope scope, IReadOnlyDictionary<Guid, PersonalBestSnapshot> personalBests, IReadOnlyDictionary<Guid, Category> categoryById) =>
        RankRows(participants, match).Select(item => CreateEntry(item.Row, item.Position, item.NeedsTieBreaker, scope, personalBests, categoryById)).ToList();

    // Ranks by the match's scoring rules (total first, then any countKey equalizers); participants still tied after the
    // last rule share a position and are flagged as needing a tie-breaker.
    private List<(RankedParticipant Row, int Position, bool NeedsTieBreaker)> RankRows(IEnumerable<MatchParticipant> participants, Match match)
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

        var ranked = new List<(RankedParticipant Row, int Position, bool NeedsTieBreaker)>();
        var position = 1;
        foreach (var bucket in buckets)
        {
            var needsTieBreaker = bucket.Count > 1;
            foreach (var row in bucket.OrderBy(item => item.Result.Name))
            {
                ranked.Add((row, position, needsTieBreaker));
            }
            position += bucket.Count;
        }
        return ranked;
    }

    private static string DisplayName(MatchParticipant participant) => string.IsNullOrWhiteSpace(participant.FullName) ? participant.LastName : participant.FullName;

    private static LiveScoringEntry CreateEntry(RankedParticipant row, int position, bool needsTieBreaker, LiveScoreScope scope, IReadOnlyDictionary<Guid, PersonalBestSnapshot> personalBests, IReadOnlyDictionary<Guid, Category> categoryById)
    {
        var hasPersonalBest = personalBests.TryGetValue(row.Participant.Id, out var personalBest) && scope.IncludePersonalBest;

        var details = new List<string>();
        var displayCategoryIds = Deserialize<List<Guid>>(scope.DisplayCategoryIdsJson) ?? [];
        if (displayCategoryIds.Count > 0)
        {
            var displayValues = displayCategoryIds
                .Select(id => categoryById.GetValueOrDefault(id)?.Values.FirstOrDefault(value => value.ValueId == row.Participant.Categories.GetValueOrDefault(id))?.Name)
                .Where(value => !string.IsNullOrWhiteSpace(value));
            var displayText = string.Join(" / ", displayValues);
            if (!string.IsNullOrWhiteSpace(displayText)) details.Add(displayText);
        }
        if (scope.IncludeGroupScores) details.Add(string.Join(", ", row.Result.GroupScores.OrderBy(item => item.Key).Select(item => item.Value)));
        if (scope.IncludeEqualizers && row.UsedEqualizers.Count > 0)
        {
            var equalizers = row.UsedEqualizers.Select(keyId => $"{row.Participant.Scores.Count(score => score.KeyId == keyId)}x{keyId}");
            details.Add($"({row.Result.Total} + {string.Join(", ", equalizers)})");
        }
        if (hasPersonalBest) details.Add($"PB: {personalBest.Score}");

        return new LiveScoringEntry(
            position,
            needsTieBreaker,
            DisplayName(row.Participant),
            details.Count == 0 ? null : string.Join(" | ", details),
            scope.IncludeAverage ? Math.Round(row.Result.Average, 2) : null,
            row.Participant.Scores.Count,
            row.Result.Total,
            hasPersonalBest && row.Result.Average > personalBest.Average);
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