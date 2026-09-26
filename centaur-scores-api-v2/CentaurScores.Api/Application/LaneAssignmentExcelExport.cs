using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace CentaurScores.Api.Application;

/// <summary>One match participant as the lane-assignment sheet needs it; category values are already resolved to names in match category order.</summary>
public sealed record LaneSheetParticipant(string? Lane, string? FederationNumber, string FullName, IReadOnlyList<string> CategoryValues);

/// <summary>A lane annotation split into its lane number and (upper-cased) letter, e.g. "12b" -> (12, 'B').</summary>
public readonly record struct ParsedLane(int Number, char Letter);

public sealed record LaneAssignmentExcelLabels(string SheetName, string Lane, string Present, string Letter, string Number, string Name, string Unassigned)
{
    // Same "only the export is concerned with translated text" convention as ParticipantListExcelLabels.
    public static LaneAssignmentExcelLabels For(string? language) => string.Equals(language, "nl", StringComparison.OrdinalIgnoreCase)
        ? new LaneAssignmentExcelLabels("Baanindeling", "Baan", "Aanwezig", "Letter", "Bondsnummer", "Naam", "Geen of ongeldige baan")
        : new LaneAssignmentExcelLabels("Lane assignments", "Lane", "Present", "Letter", "Number", "Name", "No lane or invalid lane");
}

/// <summary>
/// Builds the printable lane-assignment check-in sheet: one thick-bordered block per lane number with one row per
/// lane letter (A up to the highest letter used anywhere, so every block has the same rows), and a final block
/// listing everyone whose lane annotation is missing or not of the "number + letter" form.
/// </summary>
public static partial class LaneAssignmentExcelExport
{
    private const int LaneColumn = 1;
    private const int PresentColumn = 2;
    private const int LetterColumn = 3;
    private const int NumberColumn = 4;
    private const int NameColumn = 5;
    private const int FirstCategoryColumn = 6;

    [GeneratedRegex(@"^\s*(\d{1,6})\s*([A-Za-z])\s*$")]
    private static partial Regex LanePattern();

    public static ParsedLane? ParseLane(string? lane)
    {
        if (string.IsNullOrWhiteSpace(lane)) return null;
        var match = LanePattern().Match(lane);
        if (!match.Success) return null;
        return new ParsedLane(int.Parse(match.Groups[1].Value), char.ToUpperInvariant(match.Groups[2].Value[0]));
    }

    public static byte[] Build(string title, IReadOnlyList<string> categoryNames, IReadOnlyList<LaneSheetParticipant> participants, string? language)
    {
        var labels = LaneAssignmentExcelLabels.For(language);
        var lastColumn = NameColumn + categoryNames.Count;

        var parsed = participants.Select(participant => (Participant: participant, Lane: ParseLane(participant.Lane))).ToList();
        var assigned = parsed.Where(item => item.Lane is not null).Select(item => (item.Participant, Lane: item.Lane!.Value)).ToList();
        var unassigned = parsed.Where(item => item.Lane is null)
            .Select(item => item.Participant)
            .OrderBy(participant => participant.FullName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var maxLetter = assigned.Count == 0 ? 'A' : assigned.Max(item => item.Lane.Letter);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(labels.SheetName);

        var headers = new List<string> { labels.Lane, labels.Present, labels.Letter, labels.Number, labels.Name };
        headers.AddRange(categoryNames);
        for (var column = 0; column < headers.Count; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
        }
        var headerRange = sheet.Range(1, 1, 1, lastColumn);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

        // Row 2 stays empty: every block, including the first, is preceded by one blank row.
        var row = 3;
        foreach (var laneGroup in assigned.GroupBy(item => item.Lane.Number).OrderBy(group => group.Key))
        {
            var blockRows = new List<(char Letter, LaneSheetParticipant? Participant)>();
            for (var letter = 'A'; letter <= maxLetter; letter++)
            {
                var current = letter;
                // Two archers annotated with the same lane slot both get a row (same letter), rather than one silently disappearing.
                var inSlot = laneGroup.Where(item => item.Lane.Letter == current)
                    .Select(item => item.Participant)
                    .OrderBy(participant => participant.FullName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
                if (inSlot.Count == 0)
                {
                    blockRows.Add((current, null));
                }
                else
                {
                    blockRows.AddRange(inSlot.Select(participant => (current, (LaneSheetParticipant?)participant)));
                }
            }

            row = WriteBlock(sheet, row, laneGroup.Key.ToString(), blockRows.Select(item => (item.Letter.ToString(), item.Participant)).ToList(), lastColumn) + 1;
        }

        if (unassigned.Count > 0)
        {
            var headingRange = sheet.Range(row, 1, row, lastColumn);
            headingRange.Merge();
            headingRange.Value = labels.Unassigned;
            headingRange.Style.Font.Bold = true;
            row++;
            // The letter column shows the raw (invalid) annotation, so an organizer can see what was typed.
            WriteBlock(sheet, row, "", unassigned.Select(participant => (participant.Lane?.Trim() ?? "", (LaneSheetParticipant?)participant)).ToList(), lastColumn);
        }

        sheet.Column(LaneColumn).Width = 8;
        sheet.Column(PresentColumn).Width = 11;
        sheet.Column(LetterColumn).Width = 8;
        sheet.Column(NumberColumn).Width = 14;
        sheet.Column(NameColumn).Width = 32;
        for (var column = FirstCategoryColumn; column <= lastColumn; column++)
        {
            sheet.Column(column).Width = 18;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.PageSetup.SetRowsToRepeatAtTop(1, 1);
        sheet.PageSetup.PageOrientation = categoryNames.Count > 2 ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.Header.Center.AddText(title);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Writes one bordered block starting at <paramref name="firstRow"/> and returns the row just after it.</summary>
    private static int WriteBlock(IXLWorksheet sheet, int firstRow, string laneLabel, IReadOnlyList<(string Letter, LaneSheetParticipant? Participant)> rows, int lastColumn)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            var excelRow = firstRow + index;
            var (letter, participant) = rows[index];
            sheet.Cell(excelRow, LetterColumn).Value = letter;
            if (participant is null) continue;
            sheet.Cell(excelRow, NumberColumn).Value = participant.FederationNumber ?? "";
            sheet.Cell(excelRow, NameColumn).Value = participant.FullName;
            for (var category = 0; category < participant.CategoryValues.Count && FirstCategoryColumn + category <= lastColumn; category++)
            {
                sheet.Cell(excelRow, FirstCategoryColumn + category).Value = participant.CategoryValues[category];
            }
        }

        var lastRow = firstRow + rows.Count - 1;
        var laneRange = sheet.Range(firstRow, LaneColumn, lastRow, LaneColumn);
        if (rows.Count > 1) laneRange.Merge();
        sheet.Cell(firstRow, LaneColumn).Value = laneLabel;
        laneRange.Style.Font.Bold = true;
        laneRange.Style.Font.FontSize = 16;
        laneRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        laneRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Range(firstRow, PresentColumn, lastRow, LetterColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var block = sheet.Range(firstRow, 1, lastRow, lastColumn);
        block.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        block.Style.Border.OutsideBorder = XLBorderStyleValues.Thick;
        return lastRow + 1;
    }
}
