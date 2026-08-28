using ClosedXML.Excel;
using CentaurScores.Api.Domain;

namespace CentaurScores.Api.Application;

public sealed record PersonalBestImportRow(string FederationNumber, string Name, string Discipline, string MatchClassifier, int Score, DateOnly Date, DateTime RecordedAt);

public sealed record PersonalBestImportParseResult(IReadOnlyList<PersonalBestImportRow> Rows, IReadOnlyList<string> Warnings);

/// <summary>Thrown for file-shaped problems that should surface as a coded 400 to the caller (see ApiError).</summary>
public sealed class PersonalBestImportException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed record PersonalBestExportRow(string FederationNumber, string Name, string Discipline, string MatchClassifier, int Score, DateOnly Date);

public interface IPersonalBestExcelService
{
    PersonalBestImportParseResult ParseImport(Stream fileStream, PersonalBestImportConfig config);

    byte[] Export(PersonalBestExportConfig config, IReadOnlyList<PersonalBestExportRow> rows, DateOnly exportDate);
}

public sealed class PersonalBestExcelService : IPersonalBestExcelService
{
    public PersonalBestImportParseResult ParseImport(Stream fileStream, PersonalBestImportConfig config)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(fileStream);
        }
        catch (Exception exception) when (exception is not PersonalBestImportException)
        {
            throw new PersonalBestImportException("IMPORT_INVALID_FILE", "The uploaded file is not a valid Excel document.");
        }

        using (workbook)
        {
            var requiredColumns = new[] { config.DateColumn, config.FederationNumberColumn, config.NameColumn, config.DisciplineColumn, config.MatchClassifierColumn, config.ScoreColumn, config.UpdateDateColumn };

            IXLWorksheet? sheet = null;
            Dictionary<string, int>? headerIndex = null;
            var headerRowNumber = 0;
            foreach (var candidateSheet in workbook.Worksheets)
            {
                var lastSheetRow = candidateSheet.LastRowUsed()?.RowNumber() ?? 0;
                for (var rowNumber = 1; rowNumber <= lastSheetRow; rowNumber++)
                {
                    var candidateRow = candidateSheet.Row(rowNumber);
                    var lastHeaderColumn = candidateRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
                    if (lastHeaderColumn == 0) continue;

                    var candidateIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (var column = 1; column <= lastHeaderColumn; column++)
                    {
                        var text = candidateRow.Cell(column).GetString().Trim();
                        if (text.Length > 0) candidateIndex[text] = column;
                    }

                    if (requiredColumns.All(candidateIndex.ContainsKey))
                    {
                        sheet = candidateSheet;
                        headerIndex = candidateIndex;
                        headerRowNumber = rowNumber;
                        break;
                    }
                }

                if (headerIndex is not null) break;
            }

            if (sheet is null || headerIndex is null)
            {
                throw new PersonalBestImportException("IMPORT_UNRECOGNIZED_HEADERS", "The uploaded file's column headers were not recognized.");
            }

            var dateColumn = headerIndex[config.DateColumn];
            var numberColumn = headerIndex[config.FederationNumberColumn];
            var nameColumn = headerIndex[config.NameColumn];
            var disciplineColumn = headerIndex[config.DisciplineColumn];
            var classifierColumn = headerIndex[config.MatchClassifierColumn];
            var scoreColumn = headerIndex[config.ScoreColumn];
            var updateDateColumn = headerIndex[config.UpdateDateColumn];

            var rows = new List<PersonalBestImportRow>();
            var warnings = new List<string>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRowNumber;
            for (var rowNumber = headerRowNumber + 1; rowNumber <= lastRow; rowNumber++)
            {
                var row = sheet.Row(rowNumber);
                var federationNumber = row.Cell(numberColumn).GetString().Trim();
                var name = row.Cell(nameColumn).GetString().Trim();
                var discipline = row.Cell(disciplineColumn).GetString().Trim();
                var matchClassifier = row.Cell(classifierColumn).GetString().Trim();

                if (federationNumber.Length == 0 && name.Length == 0 && discipline.Length == 0 && matchClassifier.Length == 0 && row.Cell(scoreColumn).IsEmpty())
                {
                    continue; // fully blank row, not worth a warning
                }

                if (federationNumber.Length == 0)
                {
                    warnings.Add($"Row {rowNumber}: missing federation number, row skipped.");
                    continue;
                }
                if (name.Length == 0)
                {
                    warnings.Add($"Row {rowNumber}: missing name, row skipped.");
                    continue;
                }
                if (discipline.Length == 0 || matchClassifier.Length == 0)
                {
                    warnings.Add($"Row {rowNumber}: missing discipline or match classifier, row skipped.");
                    continue;
                }
                if (!TryReadDate(row.Cell(dateColumn), out var date))
                {
                    warnings.Add($"Row {rowNumber}: could not read a valid date, row skipped.");
                    continue;
                }
                if (!TryReadInt(row.Cell(scoreColumn), out var score))
                {
                    warnings.Add($"Row {rowNumber}: could not read a valid score, row skipped.");
                    continue;
                }

                var recordedAt = TryReadDate(row.Cell(updateDateColumn), out var updateDate) ? updateDate.ToDateTime(TimeOnly.MinValue) : DateTime.UtcNow;
                rows.Add(new PersonalBestImportRow(federationNumber, name, discipline, matchClassifier, score, date, recordedAt));
            }

            return new PersonalBestImportParseResult(rows, warnings);
        }
    }

    public byte[] Export(PersonalBestExportConfig config, IReadOnlyList<PersonalBestExportRow> rows, DateOnly exportDate)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SanitizeSheetName(config.TableName));

        var columns = config.Columns.OrderBy(item => item.SortOrder).ToList();
        for (var column = 0; column < columns.Count; column++)
        {
            sheet.Cell(1, column + 1).Value = columns[column].ColumnName;
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var excelRow = rowIndex + 2;
            for (var column = 0; column < columns.Count; column++)
            {
                sheet.Cell(excelRow, column + 1).Value = FieldValue(columns[column], row, exportDate);
            }
        }

        var lastDataRow = Math.Max(rows.Count + 1, 1);
        if (columns.Count > 0)
        {
            sheet.Range(1, 1, lastDataRow, columns.Count).CreateTable(SanitizeTableName(config.TableName));
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string FieldValue(PersonalBestExportColumn column, PersonalBestExportRow row, DateOnly exportDate) => column.Field switch
    {
        "federationNumber" => row.FederationNumber,
        "fullName" => row.Name,
        "discipline" => row.Discipline,
        "matchClassifier" => row.MatchClassifier,
        "score" => row.Score.ToString(),
        "date" => FormatDate(row.Date, column.DateFormat),
        "exportDate" => FormatDate(exportDate, column.DateFormat),
        _ => ""
    };

    private static string FormatDate(DateOnly date, string? format) => format switch
    {
        "dmy" => date.ToString("dd-MM-yyyy"),
        "mdy" => date.ToString("MM-dd-yyyy"),
        _ => date.ToString("yyyy-MM-dd")
    };

    private static bool TryReadDate(IXLCell cell, out DateOnly date)
    {
        if (cell.DataType == XLDataType.DateTime)
        {
            date = DateOnly.FromDateTime(cell.GetDateTime());
            return true;
        }

        var text = cell.GetString().Trim();
        if (DateOnly.TryParse(text, out date)) return true;
        date = default;
        return false;
    }

    private static bool TryReadInt(IXLCell cell, out int value)
    {
        if (cell.DataType == XLDataType.Number)
        {
            value = (int)Math.Round(cell.GetDouble());
            return true;
        }

        return int.TryParse(cell.GetString().Trim(), out value);
    }

    private static string SanitizeSheetName(string name)
    {
        var invalid = new[] { '\\', '/', '*', '?', ':', '[', ']' };
        var sanitized = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        if (sanitized.Length == 0) sanitized = "Export";
        return sanitized.Length > 31 ? sanitized[..31] : sanitized;
    }

    private static string SanitizeTableName(string name)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var character in name)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }
        var candidate = builder.Length == 0 ? "Export" : builder.ToString();
        if (!char.IsLetter(candidate[0]) && candidate[0] != '_') candidate = "Tbl_" + candidate;
        return candidate;
    }
}
