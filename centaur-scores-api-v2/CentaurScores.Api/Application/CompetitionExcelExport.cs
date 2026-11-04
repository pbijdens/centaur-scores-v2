using ClosedXML.Excel;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;

namespace CentaurScores.Api.Application;

public sealed record CompetitionExcelLabels(
    string ResultsSheet,
    string DetailsSheet,
    string Group,
    string Position,
    string Number,
    string Name,
    string LastName,
    string Score,
    string Arrows,
    string AveragePerArrow,
    string F1Points,
    string RoundsShot,
    string AllScore,
    string AllAveragePerArrow,
    string CountingRounds,
    string Points,
    string CountingScore,
    string CountingAveragePerArrow,
    string MinimumMet,
    string TotalPoints,
    string CountingMatches,
    string AverageMatchScore,
    string Qualified,
    string RoundNumber,
    string Round,
    string RoundName,
    string Match,
    string Date,
    string UsedForRound,
    string CountingInRules,
    string Counting,
    string KeyCount,
    string End,
    string Unknown)
{
    // Same "only the export is concerned with translated text" convention as ParticipantListExcelLabels.
    public static CompetitionExcelLabels For(string? language) => string.Equals(language, "nl", StringComparison.OrdinalIgnoreCase)
        ? new CompetitionExcelLabels(
            "Resultaten", "Details", "Groep", "Positie", "Bondsnummer", "Naam", "Achternaam",
            "score", "pijlen", "gem. per pijl", "F1-punten",
            "gespeelde rondes", "totaalscore (alle)", "gem. per pijl (alle)", "tellende rondes", "punten", "tellende score", "gem. per pijl (tellend)", "minimum gehaald",
            "Totaal punten", "Tellende wedstrijden", "Gem. wedstrijdscore (tellend)", "Gekwalificeerd",
            "Ronde #", "Ronde", "Rondenaam", "Wedstrijd", "Datum", "Gebruikt voor ronde", "Telt voor regels", "Tellend",
            "Aantal", "Serie", "Onbekend")
        : new CompetitionExcelLabels(
            "Results", "Details", "Group", "Position", "Number", "Name", "Last name",
            "score", "arrows", "avg per arrow", "F1 points",
            "rounds shot", "total score (all)", "avg per arrow (all)", "counting rounds", "points", "counting score", "avg per arrow (counting)", "minimum met",
            "Total points", "Counting matches", "Avg match score (counting)", "Qualified",
            "Round #", "Round", "Round name", "Match", "Date", "Used for round", "Counting in rules", "Counting",
            "Count", "End", "Unknown");
}

/// <summary>
/// The all-in-one competition export. Sheets, in order:
/// <list type="bullet">
/// <item>Results - one row per participating archer: per round the match score, per-arrow average and f1 points; per scoring rule the
/// figures over all rounds shot and over the counting rounds; the competition total, its per-arrow average and whether they qualified.</item>
/// <item>Data and Metadata - exactly the participant list export (see ParticipantListExcelService) for the list(s) used by the
/// competition's matches, including archers that did not take part, with the counting-match averages appended.</item>
/// <item>Details - one row per archer per match shot (including matches not used for the round), with per-key counts and end
/// scores, for analysis.</item>
/// </list>
/// </summary>
public static class CompetitionExcelExport
{
    private const string AverageFormat = "0.00";

    public static byte[] Build(CompetitionExportData data, IReadOnlyList<Category> categories, IReadOnlyList<ParticipantListMember> members, string? language)
    {
        var labels = CompetitionExcelLabels.For(language);
        var listLabels = ParticipantListExcelLabels.For(language);
        var orderedCategories = categories.OrderBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var usedTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var workbook = new XLWorkbook();
        var resultsSheet = workbook.Worksheets.Add(labels.ResultsSheet);
        WriteResultsSheet(resultsSheet, data, orderedCategories, labels, usedTableNames);

        var byParticipant = data.Participants.ToDictionary(participant => participant.ParticipantId);
        CompetitionExportParticipant? Find(ParticipantListMember member) => byParticipant.GetValueOrDefault(member.Id);
        var extraColumns = new List<ParticipantListExtraColumn>
        {
            new(labels.AverageMatchScore, member => Find(member) is { } participant && participant.CountingRoundIds.Count > 0 ? (double)participant.CountingScore / participant.CountingRoundIds.Count : Blank.Value, AverageFormat),
            new(Cap(labels.CountingAveragePerArrow), member => Find(member) is { } participant ? Average(participant.CountingScore, participant.CountingArrows) : Blank.Value, AverageFormat),
            new(labels.CountingMatches, member => Find(member)?.CountingRoundIds.Count ?? 0),
            new(labels.Qualified, member => Find(member)?.Qualified ?? false)
        };
        ParticipantListExcelService.WriteParticipantSheets(workbook, members, categories, language, extraColumns, usedTableNames);

        var detailsSheet = workbook.Worksheets.Add(labels.DetailsSheet);
        WriteDetailsSheet(detailsSheet, data, orderedCategories, labels, listLabels.Unknown, usedTableNames);

        resultsSheet.SetTabActive();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteResultsSheet(IXLWorksheet sheet, CompetitionExportData data, IReadOnlyList<Category> categories, CompetitionExcelLabels labels, HashSet<string> usedTableNames)
    {
        var columns = new List<Column<CompetitionExportParticipant>>
        {
            new(labels.Group, participant => participant.Group),
            new(labels.Position, participant => int.TryParse(participant.Position, out var position) ? position : participant.Position),
            new(labels.Number, participant => participant.FederationNumber ?? ""),
            new(labels.Name, participant => participant.Name),
            new(labels.LastName, participant => participant.LastName)
        };
        columns.AddRange(categories.Select(category => new Column<CompetitionExportParticipant>(category.Name, participant => CategoryValueName(participant.Categories, category, labels.Unknown))));

        foreach (var round in data.Rounds)
        {
            var roundName = RoundLabel(round);
            columns.Add(new($"{roundName} {labels.Score}", participant => participant.Rounds.TryGetValue(round.Id, out var score) ? score.Score : Blank.Value));
            columns.Add(new($"{roundName} {labels.AveragePerArrow}", participant => participant.Rounds.TryGetValue(round.Id, out var score) ? Average(score.Score, score.Arrows) : Blank.Value, AverageFormat));
            columns.Add(new($"{roundName} {labels.F1Points}", participant => participant.Rounds.TryGetValue(round.Id, out var score) ? score.F1Points : Blank.Value));
        }

        for (var index = 0; index < data.Rules.Count; index++)
        {
            var ruleIndex = index;
            var ruleName = data.Rules[index].Name;
            columns.Add(new($"{ruleName} {labels.RoundsShot}", participant => participant.Rules[ruleIndex].RoundsShot));
            columns.Add(new($"{ruleName} {labels.AllScore}", participant => participant.Rules[ruleIndex].AllScore));
            columns.Add(new($"{ruleName} {labels.AllAveragePerArrow}", participant => Average(participant.Rules[ruleIndex].AllScore, participant.Rules[ruleIndex].AllArrows), AverageFormat));
            columns.Add(new($"{ruleName} {labels.CountingRounds}", participant => participant.Rules[ruleIndex].CountingRounds));
            columns.Add(new($"{ruleName} {labels.Points}", participant => participant.Rules[ruleIndex].Points));
            columns.Add(new($"{ruleName} {labels.CountingScore}", participant => participant.Rules[ruleIndex].CountingScore));
            columns.Add(new($"{ruleName} {labels.CountingAveragePerArrow}", participant => Average(participant.Rules[ruleIndex].CountingScore, participant.Rules[ruleIndex].CountingArrows), AverageFormat));
            columns.Add(new($"{ruleName} {labels.MinimumMet}", participant => participant.Rules[ruleIndex].MinimumMet));
        }

        columns.Add(new(labels.TotalPoints, participant => participant.Total));
        columns.Add(new(labels.CountingMatches, participant => participant.CountingRoundIds.Count));
        columns.Add(new(Cap(labels.CountingScore), participant => participant.CountingScore));
        columns.Add(new(labels.AverageMatchScore, participant => participant.CountingRoundIds.Count > 0 ? (double)participant.CountingScore / participant.CountingRoundIds.Count : Blank.Value, AverageFormat));
        columns.Add(new(Cap(labels.CountingAveragePerArrow), participant => Average(participant.CountingScore, participant.CountingArrows), AverageFormat));
        columns.Add(new(labels.Qualified, participant => participant.Qualified));

        WriteTable(sheet, labels.ResultsSheet, columns, data.Participants, usedTableNames);
        sheet.SheetView.FreezeColumns(5);
    }

    private static void WriteDetailsSheet(IXLWorksheet sheet, CompetitionExportData data, IReadOnlyList<Category> categories, CompetitionExcelLabels labels, string unknownLabel, HashSet<string> usedTableNames)
    {
        var rows = data.Participants.SelectMany(participant => participant.Matches.Select(entry => (Participant: participant, Entry: entry))).ToList();
        var roundsById = data.Rounds.Select((round, index) => (round, index)).ToDictionary(item => item.round.Id);
        var rulesByRound = data.Rules
            .SelectMany((rule, index) => JsonRoundIds(rule.RoundIdsJson).Select(roundId => (RoundId: roundId, Index: index)))
            .GroupBy(item => item.RoundId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Index).ToList());

        var keyOrder = CompetitionService.TieBreakKeyOrder.Select((key, index) => (key, index)).ToDictionary(item => item.key, item => item.index);
        var keys = rows.SelectMany(row => row.Entry.Participant.Scores.Select(score => score.KeyId)).Distinct()
            .OrderBy(key => keyOrder.GetValueOrDefault(key, int.MaxValue))
            .ThenBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var maxEnd = rows.SelectMany(row => row.Entry.Participant.Scores.Select(score => score.End)).DefaultIfEmpty(0).Max();

        var columns = new List<Column<(CompetitionExportParticipant Participant, CompetitionExportMatchEntry Entry)>>
        {
            new(labels.Group, row => row.Participant.Group),
            new(labels.Position, row => int.TryParse(row.Participant.Position, out var position) ? position : row.Participant.Position),
            new(labels.Number, row => row.Participant.FederationNumber ?? ""),
            new(labels.Name, row => row.Participant.Name),
            new(labels.LastName, row => row.Participant.LastName)
        };
        columns.AddRange(categories.Select(category => new Column<(CompetitionExportParticipant Participant, CompetitionExportMatchEntry Entry)>(category.Name, row => CategoryValueName(row.Entry.Participant.Categories, category, unknownLabel))));
        columns.Add(new(labels.RoundNumber, row => roundsById.TryGetValue(row.Entry.RoundId, out var round) ? round.index + 1 : Blank.Value));
        columns.Add(new(labels.Round, row => roundsById.TryGetValue(row.Entry.RoundId, out var round) ? round.round.ShortName : ""));
        columns.Add(new(labels.RoundName, row => roundsById.TryGetValue(row.Entry.RoundId, out var round) ? round.round.LongName : ""));
        columns.Add(new(labels.Match, row => row.Entry.Match.Name));
        columns.Add(new(labels.Date, row => row.Entry.Match.Date.ToDateTime(TimeOnly.MinValue), "yyyy-mm-dd"));
        columns.Add(new(Cap(labels.Score), row => row.Entry.Score));
        columns.Add(new(Cap(labels.Arrows), row => row.Entry.Arrows));
        columns.Add(new(Cap(labels.AveragePerArrow), row => Average(row.Entry.Score, row.Entry.Arrows), AverageFormat));
        columns.Add(new(labels.F1Points, row => row.Entry.F1Points));
        columns.Add(new(labels.UsedForRound, row => row.Entry.UsedForRound));
        columns.Add(new(labels.CountingInRules, row => row.Entry.UsedForRound
            ? string.Join(", ", (rulesByRound.GetValueOrDefault(row.Entry.RoundId) ?? []).Where(index => IsCountingForRule(row.Participant, row.Entry.RoundId, index)).Select(index => data.Rules[index].Name))
            : ""));
        columns.Add(new(labels.Counting, row => row.Entry.UsedForRound && row.Participant.CountingRoundIds.Contains(row.Entry.RoundId)));
        columns.Add(new(labels.Qualified, row => row.Participant.Qualified));
        columns.AddRange(keys.Select(key => new Column<(CompetitionExportParticipant Participant, CompetitionExportMatchEntry Entry)>($"{labels.KeyCount} {key}", row => row.Entry.Participant.Scores.Count(score => score.KeyId == key))));
        for (var end = 1; end <= maxEnd; end++)
        {
            var endNumber = end;
            columns.Add(new($"{labels.End} {endNumber}", row => row.Entry.Participant.Scores.Any(score => score.End == endNumber)
                ? row.Entry.Participant.Scores.Where(score => score.End == endNumber).Sum(score => score.Value)
                : Blank.Value));
        }

        WriteTable(sheet, labels.DetailsSheet, columns, rows, usedTableNames);
        sheet.SheetView.FreezeColumns(5);
    }

    // Whether the round was one of the rounds the given rule actually used (its best-N selection) for this participant.
    private static bool IsCountingForRule(CompetitionExportParticipant participant, Guid roundId, int ruleIndex) =>
        participant.Rules[ruleIndex].CountingRoundIds.Contains(roundId);

    private static void WriteTable<T>(IXLWorksheet sheet, string tableName, IReadOnlyList<Column<T>> columns, IReadOnlyList<T> rows, HashSet<string> usedTableNames)
    {
        var headers = UniqueHeaders(columns.Select(column => column.Header).ToList());
        for (var column = 0; column < columns.Count; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
        }

        for (var row = 0; row < rows.Count; row++)
        {
            for (var column = 0; column < columns.Count; column++)
            {
                sheet.Cell(row + 2, column + 1).Value = columns[column].Value(rows[row]);
            }
        }

        for (var column = 0; column < columns.Count; column++)
        {
            var excelColumn = sheet.Column(column + 1);
            if (columns[column].NumberFormat is { } format)
            {
                excelColumn.Style.NumberFormat.Format = format;
            }
            excelColumn.Width = Math.Clamp(headers[column].Length + 4, 10, 40);
        }

        var name = ParticipantListExcelService.SanitizeTableName(tableName, usedTableNames);
        usedTableNames.Add(name);
        sheet.Range(1, 1, Math.Max(rows.Count + 1, 2), columns.Count).CreateTable(name);
        sheet.SheetView.FreezeRows(1);
    }

    // Excel table headers must be unique; round short names, rule names and category names are user-chosen and may collide.
    private static List<string> UniqueHeaders(IReadOnlyList<string> headers)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var header in headers)
        {
            var candidate = string.IsNullOrWhiteSpace(header) ? "-" : header.Trim();
            var baseCandidate = candidate;
            var suffix = 2;
            while (!seen.Add(candidate))
            {
                candidate = $"{baseCandidate} ({suffix++})";
            }
            result.Add(candidate);
        }
        return result;
    }

    private static string RoundLabel(CompetitionResultRound round) => string.IsNullOrWhiteSpace(round.ShortName) ? round.LongName : round.ShortName;

    private static string Cap(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static XLCellValue Average(int score, int arrows) => arrows > 0 ? (double)score / arrows : Blank.Value;

    private static string CategoryValueName(IReadOnlyDictionary<Guid, int> values, Category category, string unknownLabel) =>
        values.TryGetValue(category.Id, out var valueId) && category.Values.FirstOrDefault(value => value.ValueId == valueId) is { } match ? match.Name : unknownLabel;

    private static IReadOnlyList<Guid> JsonRoundIds(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private sealed record Column<T>(string Header, Func<T, XLCellValue> Value, string? NumberFormat = null);
}
