using ClosedXML.Excel;
using CentaurScores.Api.Application;

namespace CentaurScores.Api.Tests;

public sealed class LaneAssignmentExcelExportTests
{
    [Theory]
    [InlineData("11A", 11, 'A')]
    [InlineData(" 7b ", 7, 'B')]
    [InlineData("12 C", 12, 'C')]
    [InlineData("01D", 1, 'D')]
    public void ParseLane_accepts_number_followed_by_letter(string lane, int number, char letter)
    {
        Assert.Equal(new ParsedLane(number, letter), LaneAssignmentExcelExport.ParseLane(lane));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("A12")]
    [InlineData("12AB")]
    [InlineData("Front")]
    public void ParseLane_rejects_anything_else(string? lane)
    {
        Assert.Null(LaneAssignmentExcelExport.ParseLane(lane));
    }

    [Fact]
    public void Build_writes_one_equally_sized_block_per_lane_and_a_final_unassigned_block()
    {
        var participants = new List<LaneSheetParticipant>
        {
            new("2A", "200", "Dana Delta", ["Senior"]),
            new("1b", "100", "Bob Bravo", ["Junior"]),
            new("1C", "101", "Cara Charlie", ["Senior"]),
            new("oops", "300", "Zed Invalid", ["Senior"]),
            new(null, "301", "Amy Unassigned", [""])
        };

        var bytes = LaneAssignmentExcelExport.Build("Indoor", ["Klasse"], participants, "nl");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Baanindeling");
        Assert.Equal(["Baan", "Aanwezig", "Letter", "Bondsnummer", "Naam", "Klasse"], Enumerable.Range(1, 6).Select(column => sheet.Cell(1, column).GetString()));

        // Lane 1 block: rows 3-5, letters A..C (C is the highest letter used anywhere), A left unassigned.
        Assert.True(sheet.Cell(2, 3).IsEmpty());
        Assert.Equal("1", sheet.Cell(3, 1).GetString());
        Assert.True(sheet.Cell(3, 1).IsMerged());
        Assert.Equal(["A", "B", "C"], Enumerable.Range(3, 3).Select(row => sheet.Cell(row, 3).GetString()));
        Assert.Equal("", sheet.Cell(3, 5).GetString());
        Assert.Equal("Bob Bravo", sheet.Cell(4, 5).GetString());
        Assert.Equal("100", sheet.Cell(4, 4).GetString());
        Assert.Equal("Junior", sheet.Cell(4, 6).GetString());
        Assert.Equal("Cara Charlie", sheet.Cell(5, 5).GetString());
        Assert.Equal(XLBorderStyleValues.Thick, sheet.Cell(3, 1).Style.Border.TopBorder);
        Assert.Equal(XLBorderStyleValues.Thick, sheet.Cell(5, 6).Style.Border.BottomBorder);
        Assert.Equal(XLBorderStyleValues.Thin, sheet.Cell(4, 5).Style.Border.BottomBorder);

        // Blank separator row, then lane 2 with the same A..C rows.
        Assert.True(sheet.Row(6).IsEmpty());
        Assert.Equal("2", sheet.Cell(7, 1).GetString());
        Assert.Equal(["A", "B", "C"], Enumerable.Range(7, 3).Select(row => sheet.Cell(row, 3).GetString()));
        Assert.Equal("Dana Delta", sheet.Cell(7, 5).GetString());

        // Unassigned/invalid heading, then their own block sorted by name, showing the raw invalid lane text.
        Assert.Equal("Geen of ongeldige baan", sheet.Cell(11, 1).GetString());
        Assert.Equal("Amy Unassigned", sheet.Cell(12, 5).GetString());
        Assert.Equal("", sheet.Cell(12, 3).GetString());
        Assert.Equal("Zed Invalid", sheet.Cell(13, 5).GetString());
        Assert.Equal("oops", sheet.Cell(13, 3).GetString());
    }

    [Fact]
    public void Build_keeps_both_archers_when_two_share_a_lane_slot()
    {
        var participants = new List<LaneSheetParticipant>
        {
            new("3A", "1", "Beta", []),
            new("3A", "2", "Alpha", []),
            new("3B", "3", "Gamma", [])
        };

        var bytes = LaneAssignmentExcelExport.Build("Indoor", [], participants, "en");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Lane assignments");
        Assert.Equal(["A", "A", "B"], Enumerable.Range(3, 3).Select(row => sheet.Cell(row, 3).GetString()));
        Assert.Equal(["Alpha", "Beta", "Gamma"], Enumerable.Range(3, 3).Select(row => sheet.Cell(row, 5).GetString()));
    }
}
