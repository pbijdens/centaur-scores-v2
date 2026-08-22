using CentaurScores.Api.Application;
using CentaurScores.Api.Domain;
using System.Text.Json;

namespace CentaurScores.Api.Tests;

public sealed class CompetitionServiceTests
{
    [Fact]
    public void Calculate_disqualifies_participants_below_minimum_and_sums_highest_scores()
    {
        var round1 = Guid.NewGuid();
        var round2 = Guid.NewGuid();
        var participant = Guid.NewGuid();
        var competition = new Competition { Id = Guid.NewGuid(), ScoringRules = [new CompetitionScoreRule { Id = Guid.NewGuid(), Name = "Score 1", RoundIdsJson = JsonSerializer.Serialize(new[] { round1, round2 }), HighestScores = 1, MinimumScores = 2 }] };
        var results = new Dictionary<Guid, IReadOnlyList<ParticipantResult>>
        {
            [round1] = [new ParticipantResult(participant, "A Archer", 100, 10, new Dictionary<int, int>())],
            [round2] = []
        };
        var result = new CompetitionService(new ScoringService()).Calculate(competition, results).Single();
        Assert.True(result.Disqualified);
        Assert.Equal(100, result.Total);
        Assert.Equal(100, result.RuleScores["Score 1"]);
    }

    [Fact]
    public void BuildResults_ranks_participants_by_total_within_a_single_group()
    {
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var match = new Match
        {
            Id = matchId,
            ArrowsPerEnd = 3,
            Participants =
            [
                new MatchParticipant { Id = Guid.NewGuid(), ParticipantListMemberId = idA, FullName = "A Archer", Categories = [], Scores = [new ArrowScore { Id = Guid.NewGuid(), KeyId = "9", Value = 9 }, new ArrowScore { Id = Guid.NewGuid(), KeyId = "9", Value = 9 }, new ArrowScore { Id = Guid.NewGuid(), KeyId = "9", Value = 9 }] },
                new MatchParticipant { Id = Guid.NewGuid(), ParticipantListMemberId = idB, FullName = "B Archer", Categories = [], Scores = [new ArrowScore { Id = Guid.NewGuid(), KeyId = "6", Value = 6 }, new ArrowScore { Id = Guid.NewGuid(), KeyId = "6", Value = 6 }, new ArrowScore { Id = Guid.NewGuid(), KeyId = "6", Value = 6 }] }
            ]
        };
        var round = new CompetitionRound { Id = roundId, Order = 0, ShortName = "R1", LongName = "Round 1" };
        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            Name = "Winter Cup",
            Rounds = [round],
            ScoringRules = [new CompetitionScoreRule { Id = Guid.NewGuid(), Name = "Total", RoundIdsJson = JsonSerializer.Serialize(new[] { roundId }), HighestScores = 1, MinimumScores = 1, Aggregation = "total" }]
        };

        var document = new CompetitionService(new ScoringService()).BuildResults(competition, [], new Dictionary<Guid, IReadOnlyList<Match>> { [roundId] = [match] });

        var group = Assert.Single(document.Groups);
        Assert.Equal(2, group.Entries.Count);
        Assert.Equal("A Archer", group.Entries[0].Name);
        Assert.Equal("1", group.Entries[0].Position);
        Assert.Equal(27, group.Entries[0].Total);
        Assert.Equal("B Archer", group.Entries[1].Name);
        Assert.Equal("2", group.Entries[1].Position);
    }

    [Fact]
    public void BuildResults_uses_f1_points_when_rule_aggregation_is_f1()
    {
        var matchId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var match = new Match
        {
            Id = matchId,
            ArrowsPerEnd = 1,
            ScoringRulesJson = "[]",
            Participants =
            [
                new MatchParticipant { Id = Guid.NewGuid(), ParticipantListMemberId = idA, FullName = "A Archer", Categories = [], Scores = [new ArrowScore { Id = Guid.NewGuid(), KeyId = "10", Value = 10 }] },
                new MatchParticipant { Id = Guid.NewGuid(), ParticipantListMemberId = idB, FullName = "B Archer", Categories = [], Scores = [new ArrowScore { Id = Guid.NewGuid(), KeyId = "9", Value = 9 }] }
            ]
        };
        var round = new CompetitionRound { Id = roundId, Order = 0, ShortName = "R1", LongName = "Round 1" };
        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            Name = "F1 Cup",
            Rounds = [round],
            ScoringRules = [new CompetitionScoreRule { Id = Guid.NewGuid(), Name = "Points", RoundIdsJson = JsonSerializer.Serialize(new[] { roundId }), HighestScores = 1, MinimumScores = 1, Aggregation = "f1" }]
        };

        var document = new CompetitionService(new ScoringService()).BuildResults(competition, [], new Dictionary<Guid, IReadOnlyList<Match>> { [roundId] = [match] });

        var group = Assert.Single(document.Groups);
        Assert.Equal(12, group.Entries.Single(item => item.Name == "A Archer").RuleScores["Points"]);
        Assert.Equal(10, group.Entries.Single(item => item.Name == "B Archer").RuleScores["Points"]);
    }

    [Fact]
    public void BuildResults_uses_earliest_match_score_and_lower_score_on_a_date_tie_when_a_round_has_overlapping_matches()
    {
        var roundId = Guid.NewGuid();
        var idEarliestWins = Guid.NewGuid();
        var idTieWins = Guid.NewGuid();

        MatchParticipant Participant(Guid participantId, string name, int value) => new()
        {
            Id = Guid.NewGuid(),
            ParticipantListMemberId = participantId,
            FullName = name,
            Categories = [],
            Scores = [new ArrowScore { Id = Guid.NewGuid(), KeyId = value.ToString(), Value = value }]
        };

        var earlierMatch = new Match { Id = Guid.NewGuid(), Date = new DateOnly(2026, 1, 1), ArrowsPerEnd = 1, Participants = [Participant(idEarliestWins, "Early Archer", 5)] };
        var laterMatch = new Match { Id = Guid.NewGuid(), Date = new DateOnly(2026, 1, 8), ArrowsPerEnd = 1, Participants = [Participant(idEarliestWins, "Early Archer", 9)] };
        var sameDateHigh = new Match { Id = Guid.NewGuid(), Date = new DateOnly(2026, 2, 1), ArrowsPerEnd = 1, Participants = [Participant(idTieWins, "Tie Archer", 9)] };
        var sameDateLow = new Match { Id = Guid.NewGuid(), Date = new DateOnly(2026, 2, 1), ArrowsPerEnd = 1, Participants = [Participant(idTieWins, "Tie Archer", 3)] };

        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            Name = "Overlap Cup",
            ScoringRules = [new CompetitionScoreRule { Id = Guid.NewGuid(), Name = "Total", RoundIdsJson = JsonSerializer.Serialize(new[] { roundId }), HighestScores = 1, MinimumScores = 1, Aggregation = "total" }]
        };

        var document = new CompetitionService(new ScoringService()).BuildResults(
            competition,
            [],
            new Dictionary<Guid, IReadOnlyList<Match>> { [roundId] = [earlierMatch, laterMatch, sameDateHigh, sameDateLow] });

        var group = Assert.Single(document.Groups);
        Assert.Equal(5, group.Entries.Single(item => item.Name == "Early Archer").RuleScores["Total"]);
        Assert.Equal(3, group.Entries.Single(item => item.Name == "Tie Archer").RuleScores["Total"]);
    }
}