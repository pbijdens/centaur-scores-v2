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
        var result = new CompetitionService().Calculate(competition, results).Single();
        Assert.True(result.Disqualified);
        Assert.Equal(0, result.Total);
        Assert.Equal(100, result.RuleScores["Score 1"]);
    }
}