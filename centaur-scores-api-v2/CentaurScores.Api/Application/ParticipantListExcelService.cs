using System.Text;
using ClosedXML.Excel;
using CentaurScores.Api.Domain;

namespace CentaurScores.Api.Application;

/// <summary>
/// The small set of user-facing labels the export/import file needs, in the requesting user's language.
/// This is the only place in the API that is concerned with translated text; everything else in the
/// backend is language-agnostic and lets the frontend own presentation.
/// </summary>
public sealed record ParticipantListExcelLabels(string Number, string Name, string LastName, string Active, string Unknown)
{
    public static ParticipantListExcelLabels For(string? language) => string.Equals(language, "nl", StringComparison.OrdinalIgnoreCase)
        ? new ParticipantListExcelLabels("Bondsnummer", "Naam", "Achternaam", "Actief", "Onbekend")
        : new ParticipantListExcelLabels("Number", "Name", "Last name", "Active", "Unknown");

    public static readonly IReadOnlyList<ParticipantListExcelLabels> All = [For("en"), For("nl")];
}

public sealed record ParticipantListImportRow(string? FederationNumber, string FullName, string LastName, bool IsActive, IReadOnlyDictionary<Guid, int> Categories);

public sealed record ParticipantListImportParseResult(IReadOnlyList<ParticipantListImportRow> Rows, IReadOnlyList<string> Warnings);

/// <summary>Thrown for file-shaped problems that should surface as a coded 400 to the caller (see ApiError).</summary>
public sealed class ParticipantListImportException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public interface IParticipantListExcelService
{
    byte[] Export(ParticipantList list, IReadOnlyList<Category> categories, string? language);

    ParticipantListImportParseResult Import(Stream fileStream, IReadOnlyList<Category> categories);
}

public sealed class ParticipantListExcelService : IParticipantListExcelService
{
    public byte[] Export(ParticipantList list, IReadOnlyList<Category> categories, string? language)
    {
        var labels = ParticipantListExcelLabels.For(language);
        var orderedCategories = categories.OrderBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        using var workbook = new XLWorkbook();
        var dataSheet = workbook.Worksheets.Add("Data");

        var headers = new List<string> { labels.Number, labels.Name, labels.LastName, labels.Active };
        headers.AddRange(orderedCategories.Select(category => category.Name));
        for (var column = 0; column < headers.Count; column++)
        {
            dataSheet.Cell(1, column + 1).Value = headers[column];
        }

        // Active=false sorts last, then last name, then full name - matches the participant list screen's own ordering rule.
        var sortedMembers = list.Members
            .OrderBy(member => member.IsActive ? 0 : 1)
            .ThenBy(member => member.LastName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(member => member.FullName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        for (var row = 0; row < sortedMembers.Count; row++)
        {
            var member = sortedMembers[row];
            var excelRow = row + 2;
            dataSheet.Cell(excelRow, 1).Value = member.FederationNumber ?? "";
            dataSheet.Cell(excelRow, 2).Value = member.FullName;
            dataSheet.Cell(excelRow, 3).Value = member.LastName;
            dataSheet.Cell(excelRow, 4).Value = member.IsActive;
            for (var column = 0; column < orderedCategories.Count; column++)
            {
                dataSheet.Cell(excelRow, 5 + column).Value = CategoryValueLabel(member, orderedCategories[column], labels.Unknown);
            }
        }

        var lastDataRow = Math.Max(sortedMembers.Count + 1, 1);
        var dataRange = dataSheet.Range(1, 1, lastDataRow, headers.Count);
        dataRange.CreateTable("Data");

        var metadataSheet = workbook.Worksheets.Add("Metadata");
        var usedTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var column2 = 1;
        for (var categoryIndex = 0; categoryIndex < orderedCategories.Count; categoryIndex++)
        {
            var category = orderedCategories[categoryIndex];
            var values = category.Values
                .Select(value => value.Name)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            if (!values.Any(value => string.Equals(value, labels.Unknown, StringComparison.CurrentCultureIgnoreCase)))
            {
                values.Add(labels.Unknown);
            }

            metadataSheet.Cell(1, column2).Value = category.Name;
            for (var i = 0; i < values.Count; i++)
            {
                metadataSheet.Cell(i + 2, column2).Value = values[i];
            }

            var tableName = SanitizeTableName(category.Name, usedTableNames);
            usedTableNames.Add(tableName);
            var metadataRange = metadataSheet.Range(1, column2, values.Count + 1, column2);
            metadataRange.CreateTable(tableName);

            // Structured table references work across sheets even though a plain range reference would not,
            // which is how the Data sheet's dropdowns stay linked to this Metadata table.
            var dataColumn = 5 + categoryIndex;
            var validationRange = dataSheet.Range(2, dataColumn, 1000, dataColumn);
            var validation = validationRange.CreateDataValidation();
            validation.IgnoreBlanks = true;
            validation.InCellDropdown = true;
            validation.List($"{tableName}[{category.Name}]");

            column2 += 2;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public ParticipantListImportParseResult Import(Stream fileStream, IReadOnlyList<Category> categories)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(fileStream);
        }
        catch (Exception exception) when (exception is not ParticipantListImportException)
        {
            throw new ParticipantListImportException("IMPORT_INVALID_FILE", "The uploaded file is not a valid Excel document.");
        }

        using (workbook)
        {
            if (!workbook.TryGetWorksheet("Data", out var dataSheet))
            {
                throw new ParticipantListImportException("IMPORT_MISSING_SHEET", "The uploaded file has no 'Data' worksheet.");
            }

            var headerRow = dataSheet.Row(1);
            var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lastHeaderColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
            for (var column = 1; column <= lastHeaderColumn; column++)
            {
                var text = headerRow.Cell(column).GetString().Trim();
                if (text.Length > 0)
                {
                    headerIndex[text] = column;
                }
            }

            var labels = ParticipantListExcelLabels.All.FirstOrDefault(candidate =>
                headerIndex.ContainsKey(candidate.Number) && headerIndex.ContainsKey(candidate.Name) &&
                headerIndex.ContainsKey(candidate.LastName) && headerIndex.ContainsKey(candidate.Active));
            if (labels is null)
            {
                throw new ParticipantListImportException("IMPORT_UNRECOGNIZED_HEADERS", "The uploaded file's column headers were not recognized.");
            }

            var numberColumn = headerIndex[labels.Number];
            var nameColumn = headerIndex[labels.Name];
            var lastNameColumn = headerIndex[labels.LastName];
            var activeColumn = headerIndex[labels.Active];
            var categoryColumns = categories
                .Where(category => headerIndex.ContainsKey(category.Name))
                .ToDictionary(category => category, category => headerIndex[category.Name]);

            var rows = new List<ParticipantListImportRow>();
            var warnings = new List<string>();
            var lastRow = dataSheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var row = dataSheet.Row(rowNumber);
                var fullName = row.Cell(nameColumn).GetString().Trim();
                var lastName = row.Cell(lastNameColumn).GetString().Trim();
                var federationNumber = row.Cell(numberColumn).GetString().Trim();

                if (fullName.Length == 0)
                {
                    if (lastName.Length > 0 || federationNumber.Length > 0)
                    {
                        warnings.Add($"Row {rowNumber}: no name, row skipped.");
                    }
                    continue;
                }

                var categoryValues = new Dictionary<Guid, int>();
                foreach (var (category, column) in categoryColumns)
                {
                    var text = row.Cell(column).GetString().Trim();
                    if (text.Length == 0 || string.Equals(text, labels.Unknown, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var match = category.Values.FirstOrDefault(value => string.Equals(value.Name, text, StringComparison.OrdinalIgnoreCase));
                    if (match is null)
                    {
                        warnings.Add($"Row {rowNumber}: '{text}' is not a known value for category '{category.Name}', treated as unknown.");
                        continue;
                    }

                    categoryValues[category.Id] = match.ValueId;
                }

                rows.Add(new ParticipantListImportRow(
                    federationNumber.Length > 0 ? federationNumber : null,
                    fullName,
                    lastName.Length > 0 ? lastName : fullName,
                    ParseActive(row.Cell(activeColumn)),
                    categoryValues));
            }

            return new ParticipantListImportParseResult(rows, warnings);
        }
    }

    private static string CategoryValueLabel(ParticipantListMember member, Category category, string unknownLabel)
    {
        if (member.Categories.TryGetValue(category.Id, out var valueId))
        {
            var match = category.Values.FirstOrDefault(value => value.ValueId == valueId);
            if (match is not null)
            {
                return match.Name;
            }
        }

        return unknownLabel;
    }

    private static bool ParseActive(IXLCell cell)
    {
        if (cell.DataType == XLDataType.Boolean)
        {
            return cell.GetBoolean();
        }

        var text = cell.GetString().Trim();
        if (text.Length == 0)
        {
            return true;
        }

        return !string.Equals(text, "FALSE", StringComparison.OrdinalIgnoreCase) && text != "0";
    }

    private static string SanitizeTableName(string name, HashSet<string> used)
    {
        var builder = new StringBuilder();
        foreach (var character in name)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        var candidate = builder.Length == 0 ? "Category" : builder.ToString();
        if (!char.IsLetter(candidate[0]) && candidate[0] != '_')
        {
            candidate = "Cat_" + candidate;
        }

        var baseCandidate = candidate;
        var suffix = 1;
        while (used.Contains(candidate))
        {
            candidate = $"{baseCandidate}_{suffix}";
            suffix++;
        }

        return candidate;
    }
}
