using CentaurScores.Api.Application;
using CentaurScores.Api.Domain;

namespace CentaurScores.Api.Tests;

public sealed class LiveScoringServiceTests
{
    [Fact]
    public void BuildBlocks_groups_in_match_category_order_and_ranks_with_lazy_equalizers()
    {
        var disciplineId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var match = new Match
        {
            Id = Guid.NewGuid(),
            ArrowsPerEnd = 3,
            GroupEnds = 1,
            KeyboardJson = $$"""{"categoryOrder":["{{disciplineId}}","{{classId}}"]}""",
            ScoringRulesJson = """[{"type":"total"},{"type":"countKey","keyId":"X"},{"type":"countKey","keyId":"10"}]"""
        };
        match.Participants =
        [
            Participant("Alice", disciplineId, classId, ("X", 10), ("9", 9)),
            Participant("Bob", disciplineId, classId, ("10", 10), ("9", 9)),
            Participant("Cara", disciplineId, classId, ("10", 10), ("9", 9))
        ];
        var scope = new LiveScoreScope
        {
            GroupByCategoryIdsJson = $$"""["{{classId}}","{{disciplineId}}"]""",
            IncludeAverage = true,
            IncludeGroupScores = true,
            IncludeEqualizers = true,
            IncludePersonalBest = true
        };
        var categories = new List<Category>
        {
            new() { Id = disciplineId, Values = [new CategoryValue { CategoryId = disciplineId, ValueId = 1, Name = "Recurve" }] },
            new() { Id = classId, Values = [new CategoryValue { CategoryId = classId, ValueId = 2, Name = "Class A" }] }
        };

        var blocks = new LiveScoringService(new ScoringService()).BuildBlocks(match, scope, categories);

        var block = Assert.Single(blocks);
        Assert.Equal("Recurve, Class A", block.Name);
        Assert.Collection(block.Entries,
            entry => { Assert.Equal((1, false, "Alice"), (entry.Position, entry.NeedsTieBreaker, entry.Line1)); Assert.Contains("1xX", entry.Line2); Assert.DoesNotContain("x10", entry.Line2); },
            entry => { Assert.Equal((2, true, "Bob"), (entry.Position, entry.NeedsTieBreaker, entry.Line1)); Assert.Contains("0xX", entry.Line2); Assert.Contains("1x10", entry.Line2); },
            entry => { Assert.Equal((2, true, "Cara"), (entry.Position, entry.NeedsTieBreaker, entry.Line1)); Assert.Equal("personal best is not supported yet", entry.Line3); });
    }

    private static MatchParticipant Participant(string name, Guid disciplineId, Guid classId, params (string Key, int Value)[] scores) => new()
    {
        Id = Guid.NewGuid(),
        FullName = name,
        Categories = new Dictionary<Guid, int> { [disciplineId] = 1, [classId] = 2 },
        Scores = scores.Select((score, index) => new ArrowScore { Id = Guid.NewGuid(), End = 1, Arrow = index + 1, KeyId = score.Key, Value = score.Value }).ToList()
    };
}