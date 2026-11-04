using ClosedXML.Excel;
using CentaurScores.Api.Application;
using CentaurScores.Api.Domain;
using System.Text.Json;

namespace CentaurScores.Api.Tests;

public sealed class CompetitionExcelExportTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static MatchParticipant Shooter(ParticipantListMember member, params int[] arrows) => new()
    {
        Id = Guid.NewGuid(),
        ParticipantListMemberId = member.Id,
        ParticipantListMember = member,
        Scores = arrows.Select((value, index) => new ArrowScore { Id = Guid.NewGuid(), End = index / 3 + 1, Arrow = index % 3 + 1, KeyId = value.ToString(), Value = value }).ToList()
    };

    private static Dictionary<string, int> HeaderIndex(IXLWorksheet sheet)
    {
        var row = sheet.Row(1);
        var last = row.LastCellUsed()!.Address.ColumnNumber;
        return Enumerable.Range(1, last).ToDictionary(column => row.Cell(column).GetString(), column => column);
    }

    private static IXLRow RowFor(IXLWorksheet sheet, string nameHeader, string name)
    {
        var column = HeaderIndex(sheet)[nameHeader];
        return sheet.RowsUsed().Single(row => row.Cell(column).GetString() == name);
    }

    [Fact]
    public void Build_writes_results_participant_list_with_averages_and_details()
    {
        var listId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var category = new Category
        {
            Id = categoryId,
            TenantId = TenantId,
            Name = "Klasse",
            Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = TenantId, CategoryId = categoryId, ValueId = 1, Name = "Senior" }]
        };
        ParticipantListMember Member(string name, string last) => new() { Id = Guid.NewGuid(), TenantId = TenantId, ParticipantListId = listId, FullName = name, LastName = last, Categories = new() { [categoryId] = 1 } };
        var amy = Member("Amy Archer", "Archer");
        var bob = Member("Bob Bowman", "Bowman");
        var cat = Member("Cat Absent", "Absent");

        var round1 = new CompetitionRound { Id = Guid.NewGuid(), Order = 0, ShortName = "R1", LongName = "Round 1" };
        var round2 = new CompetitionRound { Id = Guid.NewGuid(), Order = 1, ShortName = "R2", LongName = "Round 2" };
        var match1 = new Match { Id = Guid.NewGuid(), Name = "Match 1", Date = new DateOnly(2026, 1, 1), ArrowsPerEnd = 3, ParticipantListId = listId, Participants = [Shooter(amy, 10, 10, 10), Shooter(bob, 8, 8, 8)] };
        var match2 = new Match { Id = Guid.NewGuid(), Name = "Match 2", Date = new DateOnly(2026, 2, 1), ArrowsPerEnd = 3, ParticipantListId = listId, Participants = [Shooter(amy, 9, 9, 9, 9, 9, 9)] };
        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            Name = "Winter Cup",
            Rounds = [round1, round2],
            ScoringRules =
            [
                // Best 1 of 2, at least 2 shot: Bob shot only one round, so he does not qualify.
                new CompetitionScoreRule { Id = Guid.NewGuid(), Name = "Best", RoundIdsJson = JsonSerializer.Serialize(new[] { round1.Id, round2.Id }), HighestScores = 1, MinimumScores = 2, Aggregation = "total", SortOrder = 0 }
            ]
        };
        var matchesByRound = new Dictionary<Guid, IReadOnlyList<Match>> { [round1.Id] = [match1], [round2.Id] = [match2] };

        var data = new CompetitionService(new ScoringService()).BuildExport(competition, [category], matchesByRound);
        var bytes = CompetitionExcelExport.Build(data, [category], [amy, bob, cat], "en");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(["Results", "Data", "Metadata", "Details"], workbook.Worksheets.Select(sheet => sheet.Name));

        // Results: per-round score/average/f1 and per-rule figures. Amy's best round is R2 (54 > 30), and counts.
        var results = workbook.Worksheet("Results");
        var resultColumns = HeaderIndex(results);
        var amyResult = RowFor(results, "Name", "Amy Archer");
        Assert.Equal(30, amyResult.Cell(resultColumns["R1 score"]).GetValue<int>());
        Assert.Equal(10.0, amyResult.Cell(resultColumns["R1 avg per arrow"]).GetValue<double>());
        Assert.Equal(12, amyResult.Cell(resultColumns["R1 F1 points"]).GetValue<int>());
        Assert.Equal(54, amyResult.Cell(resultColumns["R2 score"]).GetValue<int>());
        Assert.Equal(2, amyResult.Cell(resultColumns["Best rounds shot"]).GetValue<int>());
        Assert.Equal(84, amyResult.Cell(resultColumns["Best total score (all)"]).GetValue<int>());
        Assert.Equal(84.0 / 9, amyResult.Cell(resultColumns["Best avg per arrow (all)"]).GetValue<double>(), 6);
        Assert.Equal(54, amyResult.Cell(resultColumns["Best points"]).GetValue<int>());
        Assert.Equal(9.0, amyResult.Cell(resultColumns["Best avg per arrow (counting)"]).GetValue<double>());
        Assert.Equal(54, amyResult.Cell(resultColumns["Total points"]).GetValue<int>());
        Assert.Equal(9.0, amyResult.Cell(resultColumns["Avg per arrow (counting)"]).GetValue<double>());
        Assert.True(amyResult.Cell(resultColumns["Qualified"]).GetBoolean());
        var bobResult = RowFor(results, "Name", "Bob Bowman");
        Assert.True(bobResult.Cell(resultColumns["R2 score"]).IsEmpty());
        Assert.False(bobResult.Cell(resultColumns["Qualified"]).GetBoolean());

        // Data: the participant list export's columns, then the appended competition columns - including the absent archer.
        var dataSheet = workbook.Worksheet("Data");
        var dataHeaders = HeaderIndex(dataSheet).OrderBy(pair => pair.Value).Select(pair => pair.Key).ToList();
        Assert.Equal(["Number", "Name", "Last name", "Active", "Klasse", "Avg match score (counting)", "Avg per arrow (counting)", "Counting matches", "Qualified"], dataHeaders);
        var amyData = RowFor(dataSheet, "Name", "Amy Archer");
        Assert.Equal(54.0, amyData.Cell(6).GetValue<double>());
        Assert.Equal(9.0, amyData.Cell(7).GetValue<double>());
        Assert.Equal(1, amyData.Cell(8).GetValue<int>());
        Assert.True(amyData.Cell(9).GetBoolean());
        var catData = RowFor(dataSheet, "Name", "Cat Absent");
        Assert.Equal("Senior", catData.Cell(5).GetString());
        Assert.True(catData.Cell(6).IsEmpty());
        Assert.Equal(0, catData.Cell(8).GetValue<int>());
        Assert.False(catData.Cell(9).GetBoolean());

        // The participant sheets stay importable as a participant list.
        using var reimport = new MemoryStream(bytes);
        var imported = new ParticipantListExcelService().Import(reimport, [category]);
        Assert.Equal(3, imported.Rows.Count);
        Assert.Empty(imported.Warnings);

        // Details: one row per archer per match shot, with counting flag and per-end scores.
        var details = workbook.Worksheet("Details");
        var detailColumns = HeaderIndex(details);
        Assert.Equal(4, details.RowsUsed().Count()); // header + 3 match entries
        var amyR1 = details.RowsUsed().Single(row => row.Cell(detailColumns["Name"]).GetString() == "Amy Archer" && row.Cell(detailColumns["Round"]).GetString() == "R1");
        Assert.False(amyR1.Cell(detailColumns["Counting"]).GetBoolean());
        Assert.Equal(3, amyR1.Cell(detailColumns["Count 10"]).GetValue<int>());
        var amyR2 = details.RowsUsed().Single(row => row.Cell(detailColumns["Name"]).GetString() == "Amy Archer" && row.Cell(detailColumns["Round"]).GetString() == "R2");
        Assert.True(amyR2.Cell(detailColumns["Counting"]).GetBoolean());
        Assert.Equal("Best", amyR2.Cell(detailColumns["Counting in rules"]).GetString());
        Assert.Equal(27, amyR2.Cell(detailColumns["End 2"]).GetValue<int>());
    }

    [Fact]
    public void BuildExport_reports_raw_score_averages_for_the_rounds_an_f1_rule_counted()
    {
        var listId = Guid.NewGuid();
        ParticipantListMember Member(string name) => new() { Id = Guid.NewGuid(), TenantId = TenantId, ParticipantListId = listId, FullName = name, LastName = name };
        var amy = Member("Amy");
        var bob = Member("Bob");
        var round1 = new CompetitionRound { Id = Guid.NewGuid(), Order = 0, ShortName = "R1" };
        var round2 = new CompetitionRound { Id = Guid.NewGuid(), Order = 1, ShortName = "R2" };
        // R1: Amy wins with 27 (12 pts). R2: Amy loses with 30 vs Bob's 33 (10 pts). Best-1 f1 picks R1 even though R2's raw score is higher.
        var match1 = new Match { Id = Guid.NewGuid(), Date = new DateOnly(2026, 1, 1), ArrowsPerEnd = 3, Participants = [Shooter(amy, 9, 9, 9), Shooter(bob, 1, 1, 1)] };
        var match2 = new Match { Id = Guid.NewGuid(), Date = new DateOnly(2026, 2, 1), ArrowsPerEnd = 3, Participants = [Shooter(amy, 10, 10, 10), Shooter(bob, 11, 11, 11)] };
        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            Rounds = [round1, round2],
            ScoringRules = [new CompetitionScoreRule { Id = Guid.NewGuid(), Name = "F1", RoundIdsJson = JsonSerializer.Serialize(new[] { round1.Id, round2.Id }), HighestScores = 1, MinimumScores = 1, Aggregation = "f1" }]
        };

        var data = new CompetitionService(new ScoringService()).BuildExport(competition, [], new Dictionary<Guid, IReadOnlyList<Match>> { [round1.Id] = [match1], [round2.Id] = [match2] });

        var amyData = data.Participants.Single(item => item.Name == "Amy");
        var rule = Assert.Single(amyData.Rules);
        Assert.Equal(12, rule.Points);
        Assert.Equal(27, rule.CountingScore);
        Assert.Equal(3, rule.CountingArrows);
        Assert.Equal(57, rule.AllScore);
        Assert.Equal([round1.Id], rule.CountingRoundIds);
        Assert.Equal(10, amyData.Rounds[round2.Id].F1Points);
    }
}
