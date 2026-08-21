using CentaurScores.Api.Application;
using CentaurScores.Api.Domain;

namespace CentaurScores.Api.Tests;

public sealed class ScoringServiceTests
{
    [Fact]
    public void Calculate_groups_scores_and_computes_average()
    {
        var participantId = Guid.NewGuid();
        var participant = new MatchParticipant
        {
            Id = participantId,
            FullName = "A. Archer",
            Scores = [
            new ArrowScore { Id = Guid.NewGuid(), End = 1, Arrow = 1, Value = 10 },
            new ArrowScore { Id = Guid.NewGuid(), End = 1, Arrow = 2, Value = 9 },
            new ArrowScore { Id = Guid.NewGuid(), End = 2, Arrow = 1, Value = 8 }
        ]
        };
        var result = new ScoringService().Calculate(participant, 3, 2);
        Assert.Equal(27, result.Total);
        Assert.Equal(9, result.Average);
        Assert.Equal(27, result.GroupScores[1]);
    }

    [Fact]
    public void Rank_orders_by_total_then_name()
    {
        var match = new Match { Id = Guid.NewGuid(), ArrowsPerEnd = 3 };
        var first = new MatchParticipant { Id = Guid.NewGuid(), FullName = "B Archer", Scores = [new ArrowScore { Id = Guid.NewGuid(), Value = 10 }] };
        var second = new MatchParticipant { Id = Guid.NewGuid(), FullName = "A Archer", Scores = [new ArrowScore { Id = Guid.NewGuid(), Value = 10 }] };
        var result = new ScoringService().Rank([first, second], match);
        Assert.Equal("A Archer", result[0].Name);
    }
}