using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using System.Text.Json;

namespace CentaurScores.Api.Application;

public sealed record CompetitionParticipantResult(Guid ParticipantId, string Name, int Total, bool Disqualified, IReadOnlyDictionary<string, int> RuleScores, IReadOnlyDictionary<string, IReadOnlySet<Guid>>? UsedRoundIdsByRule = null, IReadOnlyList<CompetitionResultRule>? Rules = null);

/// <summary>One match a participant shot in a competition round (a participant can be in several matches of one round; only one is used for the round).</summary>
public sealed record CompetitionExportMatchEntry(Guid RoundId, Match Match, MatchParticipant Participant, int Score, int Arrows, int F1Points, bool UsedForRound);

/// <summary>The result that counts for a participant in one round: the raw match score, the arrows shot and the f1-style points.</summary>
public sealed record CompetitionExportRoundScore(Guid MatchId, int Score, int Arrows, int F1Points);

/// <summary>
/// One participant's figures for one competition scoring rule. "All" figures cover every round of the rule the participant shot;
/// "counting" figures only the rounds the rule actually used (the N best by the rule's own aggregation). Points is what the rule
/// contributes to the competition total - the sum of the counting match scores, or of the counting f1 points for f1 rules.
/// </summary>
public sealed record CompetitionExportRuleSummary(int RoundsShot, int AllScore, int AllArrows, int CountingRounds, int Points, int CountingScore, int CountingArrows, bool MinimumMet, IReadOnlySet<Guid> CountingRoundIds);

public sealed record CompetitionExportParticipant(
    Guid ParticipantId,
    string Name,
    string LastName,
    string? FederationNumber,
    IReadOnlyDictionary<Guid, int> Categories,
    string Group,
    string Position,
    bool Qualified,
    int Total,
    IReadOnlyDictionary<Guid, CompetitionExportRoundScore> Rounds,
    IReadOnlyList<CompetitionExportRuleSummary> Rules,
    IReadOnlySet<Guid> CountingRoundIds,
    int CountingScore,
    int CountingArrows,
    IReadOnlyList<CompetitionExportMatchEntry> Matches);

/// <summary>Everything the competition Excel export needs; Rules are in SortOrder and line up with each participant's Rules.</summary>
public sealed record CompetitionExportData(Competition Competition, IReadOnlyList<CompetitionResultRound> Rounds, IReadOnlyList<CompetitionScoreRule> Rules, IReadOnlyList<CompetitionExportParticipant> Participants);

public interface ICompetitionService
{
    IReadOnlyList<CompetitionParticipantResult> Calculate(Competition competition, IReadOnlyDictionary<Guid, IReadOnlyList<ParticipantResult>> totalResultsByRound, IReadOnlyDictionary<Guid, IReadOnlyList<ParticipantResult>>? f1ResultsByRound = null);
    CompetitionResultsDocument BuildResults(Competition competition, IReadOnlyList<Category> categories, IReadOnlyDictionary<Guid, IReadOnlyList<Match>> matchesByRound);
    CompetitionExportData BuildExport(Competition competition, IReadOnlyList<Category> categories, IReadOnlyDictionary<Guid, IReadOnlyList<Match>> matchesByRound);
}

public sealed class CompetitionService(IScoringService scoringService) : ICompetitionService
{
    // Position -> f1-style points for #1..#9; every lower position with a non-zero score earns 1 point.
    private static readonly int[] F1PointsTable = [12, 10, 8, 7, 6, 5, 4, 3, 2];

    // Tie-break precedence for the final competition standings: count of X, then 10, then 9, ... down to 1.
    internal static readonly string[] TieBreakKeyOrder = ["X", "10", "9", "8", "7", "6", "5", "4", "3", "2", "1"];

    public IReadOnlyList<CompetitionParticipantResult> Calculate(Competition competition, IReadOnlyDictionary<Guid, IReadOnlyList<ParticipantResult>> totalResultsByRound, IReadOnlyDictionary<Guid, IReadOnlyList<ParticipantResult>>? f1ResultsByRound = null)
    {
        var namesByParticipant = totalResultsByRound.Values.SelectMany(value => value).GroupBy(item => item.ParticipantId).ToDictionary(group => group.Key, group => group.First().Name);
        var roundOrder = competition.Rounds.OrderBy(item => item.Order).Select(item => item.Id).ToList();

        return namesByParticipant.Keys.Select(participantId =>
        {
            var ruleScores = new Dictionary<string, int>();
            var usedRoundIdsByRule = new Dictionary<string, IReadOnlySet<Guid>>();
            var rules = new List<CompetitionResultRule>();
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
                var usedRoundIds = used.Select(item => item.RoundId).ToHashSet();
                usedRoundIdsByRule[rule.Name] = usedRoundIds;
                var valueByRound = perRound.ToDictionary(item => item.RoundId, item => item.Total);
                var ruleRounds = roundOrder.Where(roundIds.Contains)
                    .Select(roundId => new CompetitionResultRuleRound(roundId, valueByRound.TryGetValue(roundId, out var value) ? value : null, usedRoundIds.Contains(roundId)))
                    .ToList();
                rules.Add(new CompetitionResultRule(rule.Name, rule.Aggregation, ruleScores[rule.Name], ruleRounds));
            }
            return new CompetitionParticipantResult(participantId, namesByParticipant[participantId], ruleScores.Values.Sum(), disqualified, ruleScores, usedRoundIdsByRule, rules);
        }).OrderByDescending(item => item.Disqualified ? int.MinValue : item.Total).ThenByDescending(item => item.Total).ThenBy(item => item.Name).ToList();
    }

    public CompetitionResultsDocument BuildResults(Competition competition, IReadOnlyList<Category> categories, IReadOnlyDictionary<Guid, IReadOnlyList<Match>> matchesByRound) =>
        Compute(competition, categories, matchesByRound).Document;

    public CompetitionExportData BuildExport(Competition competition, IReadOnlyList<Category> categories, IReadOnlyDictionary<Guid, IReadOnlyList<Match>> matchesByRound)
    {
        var computed = Compute(competition, categories, matchesByRound);
        var groupCategoryIds = JsonIds(competition.GroupByCategoryIdsJson).ToList();
        var orderedRules = competition.ScoringRules.OrderBy(item => item.SortOrder).ToList();
        var ruleResultsById = computed.RuleResults.ToDictionary(item => item.ParticipantId);
        var f1PointsByRound = computed.F1ResultsByRound.ToDictionary(pair => pair.Key, pair => pair.Value.ToDictionary(item => item.ParticipantId, item => item.Total));

        // The match chosen for each participant in each round (see ChooseRoundMatches), plus every match they shot, for the detail sheet.
        var chosenByRound = matchesByRound.ToDictionary(pair => pair.Key, pair => ChooseRoundMatches(pair.Value));
        var f1PointsByMatch = new Dictionary<Guid, Dictionary<Guid, int>>();
        var matchEntriesByParticipant = new Dictionary<Guid, List<CompetitionExportMatchEntry>>();
        var roundOrder = computed.Document.Rounds.Select((round, index) => (round.Id, index)).ToDictionary(item => item.Id, item => item.index);
        foreach (var (roundId, matches) in matchesByRound.OrderBy(pair => roundOrder.GetValueOrDefault(pair.Key, int.MaxValue)))
        {
            var chosen = chosenByRound[roundId];
            foreach (var match in matches.OrderBy(item => item.Date).ThenBy(item => item.Name))
            {
                if (!f1PointsByMatch.TryGetValue(match.Id, out var matchPoints))
                {
                    matchPoints = ComputeMatchF1Points(match, groupCategoryIds);
                    f1PointsByMatch[match.Id] = matchPoints;
                }
                foreach (var participant in match.Participants.Where(item => item.ParticipantListMemberId is not null))
                {
                    var participantId = participant.ParticipantListMemberId!.Value;
                    var result = scoringService.Calculate(participant, match.ArrowsPerEnd, match.GroupEnds);
                    var used = chosen.TryGetValue(participantId, out var pick) && ReferenceEquals(pick.Participant, participant);
                    if (!matchEntriesByParticipant.TryGetValue(participantId, out var list))
                    {
                        list = [];
                        matchEntriesByParticipant[participantId] = list;
                    }
                    list.Add(new CompetitionExportMatchEntry(roundId, match, participant, result.Total, participant.Scores.Count, matchPoints.GetValueOrDefault(participantId), used));
                }
            }
        }

        var participants = new List<CompetitionExportParticipant>();
        foreach (var group in computed.Document.Groups)
        {
            foreach (var entry in group.Entries)
            {
                var participantId = entry.ParticipantId!.Value;
                var ruleResult = ruleResultsById[participantId];
                var rounds = new Dictionary<Guid, CompetitionExportRoundScore>();
                foreach (var (roundId, chosen) in chosenByRound)
                {
                    if (!chosen.TryGetValue(participantId, out var pick)) continue;
                    var f1Points = f1PointsByRound.GetValueOrDefault(roundId)?.GetValueOrDefault(participantId) ?? 0;
                    rounds[roundId] = new CompetitionExportRoundScore(pick.Match.Id, pick.Result.Total, pick.Participant.Scores.Count, f1Points);
                }

                var ruleSummaries = new List<CompetitionExportRuleSummary>();
                var countingRoundIds = new HashSet<Guid>();
                for (var index = 0; index < orderedRules.Count; index++)
                {
                    var rule = orderedRules[index];
                    var ruleRoundIds = JsonIds(rule.RoundIdsJson);
                    var shot = rounds.Where(pair => ruleRoundIds.Contains(pair.Key)).Select(pair => pair.Value).ToList();
                    var ruleRounds = ruleResult.Rules is { } resultRules && index < resultRules.Count ? resultRules[index] : null;
                    var counting = (ruleRounds?.Rounds ?? []).Where(item => item.Used && rounds.ContainsKey(item.RoundId)).Select(item => item.RoundId).ToList();
                    countingRoundIds.UnionWith(counting);
                    var countingScores = counting.Select(roundId => rounds[roundId]).ToList();
                    ruleSummaries.Add(new CompetitionExportRuleSummary(
                        shot.Count,
                        shot.Sum(item => item.Score),
                        shot.Sum(item => item.Arrows),
                        counting.Count,
                        ruleRounds?.Total ?? 0,
                        countingScores.Sum(item => item.Score),
                        countingScores.Sum(item => item.Arrows),
                        shot.Count >= rule.MinimumScores,
                        counting.ToHashSet()));
                }

                // A round used by more than one rule is still a single match: count it once for the overall averages.
                var countingRounds = countingRoundIds.Select(roundId => rounds[roundId]).ToList();
                var matchEntries = matchEntriesByParticipant.GetValueOrDefault(participantId) ?? [];
                var firstParticipant = matchEntries.FirstOrDefault()?.Participant;
                participants.Add(new CompetitionExportParticipant(
                    participantId,
                    entry.Name,
                    firstParticipant?.LastName ?? "",
                    firstParticipant?.FederationNumber,
                    computed.CategoriesByParticipant.GetValueOrDefault(participantId) ?? new Dictionary<Guid, int>(),
                    group.Name,
                    entry.Position ?? "-",
                    !entry.Disqualified,
                    entry.Total ?? 0,
                    rounds,
                    ruleSummaries,
                    countingRoundIds,
                    countingRounds.Sum(item => item.Score),
                    countingRounds.Sum(item => item.Arrows),
                    matchEntries));
            }
        }

        return new CompetitionExportData(competition, computed.Document.Rounds, orderedRules, participants);
    }

    private sealed record ResultsComputation(
        CompetitionResultsDocument Document,
        IReadOnlyList<CompetitionParticipantResult> RuleResults,
        IReadOnlyDictionary<Guid, IReadOnlyList<ParticipantResult>> F1ResultsByRound,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, int>> CategoriesByParticipant);

    private ResultsComputation Compute(Competition competition, IReadOnlyList<Category> categories, IReadOnlyDictionary<Guid, IReadOnlyList<Match>> matchesByRound)
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

        var document = new CompetitionResultsDocument(competition.Name, competition.StartDate, competition.EndDate, rounds, groups);
        return new ResultsComputation(
            document,
            ruleResults,
            f1ResultsByRound.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<ParticipantResult>)pair.Value),
            categoriesByParticipant);
    }

    private IReadOnlyList<ParticipantResult> BuildRoundTotals(IReadOnlyList<Match> matches)
    {
        return ChooseRoundMatches(matches)
            .Select(pair =>
            {
                var chosen = pair.Value;
                var name = chosen.Participant.FullName;
                var displayName = string.IsNullOrWhiteSpace(name) ? chosen.Participant.LastName : name;
                return new ParticipantResult(pair.Key, displayName, chosen.Result.Total, chosen.Result.Average, new Dictionary<int, int>());
            }).ToList();
    }

    private Dictionary<Guid, (Match Match, MatchParticipant Participant, ParticipantResult Result)> ChooseRoundMatches(IReadOnlyList<Match> matches)
    {
        // A participant is only scored once per round even if assigned to multiple matches in it: take the
        // earliest-dated match (archers cannot improve an already-recorded result later), and on a tie use
        // the lower score.
        return matches
            .SelectMany(match => match.Participants.Where(item => item.ParticipantListMemberId is not null).Select(item => (Match: match, Participant: item)))
            .GroupBy(item => item.Participant.ParticipantListMemberId!.Value)
            .ToDictionary(group => group.Key, group => group
                .Select(item => (item.Match, item.Participant, Result: scoringService.Calculate(item.Participant, item.Match.ArrowsPerEnd, item.Match.GroupEnds)))
                .OrderBy(item => item.Match.Date)
                .ThenBy(item => item.Result.Total)
                .First());
    }

    private IReadOnlyList<ParticipantResult> BuildRoundF1Points(IReadOnlyList<Match> matches, IReadOnlyList<Guid> groupCategoryIds)
    {
        var chosen = new Dictionary<Guid, (Match Match, int RawTotal, int Points, string Name)>();
        foreach (var match in matches)
        {
            var pointsByParticipant = ComputeMatchF1Points(match, groupCategoryIds);
            foreach (var participant in match.Participants.Where(item => item.ParticipantListMemberId is not null))
            {
                var participantId = participant.ParticipantListMemberId!.Value;
                if (!pointsByParticipant.TryGetValue(participantId, out var points)) continue;
                var rawTotal = scoringService.Calculate(participant, match.ArrowsPerEnd, match.GroupEnds).Total;
                var displayName = string.IsNullOrWhiteSpace(participant.FullName) ? participant.LastName : participant.FullName;
                // Same round, multiple matches: keep only the earliest-dated match's points; on a tie, the lower score.
                if (!chosen.TryGetValue(participantId, out var existing) || match.Date < existing.Match.Date || (match.Date == existing.Match.Date && rawTotal < existing.RawTotal))
                {
                    chosen[participantId] = (match, rawTotal, points, displayName);
                }
            }
        }
        return chosen.Select(pair => new ParticipantResult(pair.Key, pair.Value.Name, pair.Value.Points, 0, new Dictionary<int, int>())).ToList();
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

        foreach (var participant in participants.Where(item => item.Disqualified).OrderByDescending(item => item.Total).ThenBy(item => item.Name))
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
        return new CompetitionResultEntry(participant.Disqualified ? "-" : position, needsTieBreaker, participant.Name, participant.Disqualified, participant.Total, roundScores, participant.RuleScores, participant.Rules ?? [], participant.ParticipantId);
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