using ClosedXML.Excel;
using CentaurScores.Api.Application;
using CentaurScores.Api.Domain;

namespace CentaurScores.Api.Tests;

public sealed class PersonalBestExcelServiceTests
{
    private static readonly PersonalBestImportConfig Config = new();

    private static Stream BuildWorkbook(params (string Bondsnummer, string Naam, string Discipline, string Wedstrijd, string Score, string Datum, string Toegevoegd)[] rows)
        => BuildWorkbook("SomeArbitraryTabName", headerRow: 1, rows);

    private static Stream BuildWorkbook(string tabName, int headerRow, params (string Bondsnummer, string Naam, string Discipline, string Wedstrijd, string Score, string Datum, string Toegevoegd)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(tabName);
        string[] headers = [Config.FederationNumberColumn, Config.NameColumn, Config.DisciplineColumn, Config.MatchClassifierColumn, Config.ScoreColumn, Config.DateColumn, Config.UpdateDateColumn];
        for (var column = 0; column < headers.Length; column++) sheet.Cell(headerRow, column + 1).Value = headers[column];
        for (var row = 0; row < rows.Length; row++)
        {
            var (bondsnummer, naam, discipline, wedstrijd, score, datum, toegevoegd) = rows[row];
            sheet.Cell(headerRow + row + 1, 1).Value = bondsnummer;
            sheet.Cell(headerRow + row + 1, 2).Value = naam;
            sheet.Cell(headerRow + row + 1, 3).Value = discipline;
            sheet.Cell(headerRow + row + 1, 4).Value = wedstrijd;
            sheet.Cell(headerRow + row + 1, 5).Value = score;
            sheet.Cell(headerRow + row + 1, 6).Value = datum;
            sheet.Cell(headerRow + row + 1, 7).Value = toegevoegd;
        }
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void ParseImport_reads_valid_rows_and_warns_on_bad_ones()
    {
        using var stream = BuildWorkbook(
            ("123", "Robin Archer", "Recurve", "Outdoor", "500", "2026-01-01", "2026-01-05"),
            ("", "No Number", "Recurve", "Outdoor", "400", "2026-01-01", "2026-01-05"),
            ("124", "Bad Score", "Recurve", "Outdoor", "not-a-number", "2026-01-01", "2026-01-05"));

        var result = new PersonalBestExcelService().ParseImport(stream, Config);

        var row = Assert.Single(result.Rows);
        Assert.Equal("123", row.FederationNumber);
        Assert.Equal("Robin Archer", row.Name);
        Assert.Equal(500, row.Score);
        Assert.Equal(new DateOnly(2026, 1, 1), row.Date);
        Assert.Equal(2, result.Warnings.Count);
    }

    [Fact]
    public void ParseImport_ignores_the_worksheet_tab_name_and_finds_the_headers_wherever_they_are()
    {
        using var stream = BuildWorkbook("Blad1", headerRow: 1,
            ("123", "Robin Archer", "Recurve", "Outdoor", "500", "2026-01-01", "2026-01-05"));

        var result = new PersonalBestExcelService().ParseImport(stream, Config);

        var row = Assert.Single(result.Rows);
        Assert.Equal("123", row.FederationNumber);
    }

    [Fact]
    public void ParseImport_finds_headers_that_are_not_on_the_first_row()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Resultaten");
        sheet.Cell(1, 1).Value = "Overzicht persoonlijke records";
        string[] headers = [Config.FederationNumberColumn, Config.NameColumn, Config.DisciplineColumn, Config.MatchClassifierColumn, Config.ScoreColumn, Config.DateColumn, Config.UpdateDateColumn];
        for (var column = 0; column < headers.Length; column++) sheet.Cell(4, column + 1).Value = headers[column];
        sheet.Cell(5, 1).Value = "123";
        sheet.Cell(5, 2).Value = "Robin Archer";
        sheet.Cell(5, 3).Value = "Recurve";
        sheet.Cell(5, 4).Value = "Outdoor";
        sheet.Cell(5, 5).Value = "500";
        sheet.Cell(5, 6).Value = "2026-01-01";
        sheet.Cell(5, 7).Value = "2026-01-05";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var result = new PersonalBestExcelService().ParseImport(stream, Config);

        var row = Assert.Single(result.Rows);
        Assert.Equal("123", row.FederationNumber);
        Assert.Equal(500, row.Score);
    }

    [Fact]
    public void ParseImport_throws_a_coded_exception_when_headers_are_not_recognized()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(Config.TableName);
        sheet.Cell(1, 1).Value = "SomeOtherColumn";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var exception = Assert.Throws<PersonalBestImportException>(() => new PersonalBestExcelService().ParseImport(stream, Config));
        Assert.Equal("IMPORT_UNRECOGNIZED_HEADERS", exception.Code);
    }

    [Fact]
    public void Export_writes_one_row_per_entry_using_the_configured_column_order_and_date_format()
    {
        var config = new PersonalBestExportConfig
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            TableName = "Export",
            Columns =
            [
                new PersonalBestExportColumn { Id = Guid.NewGuid(), SortOrder = 1, ColumnName = "Bondsnummer", Field = "federationNumber" },
                new PersonalBestExportColumn { Id = Guid.NewGuid(), SortOrder = 0, ColumnName = "Datum", Field = "date", DateFormat = "dmy" }
            ]
        };
        var rows = new[] { new PersonalBestExportRow("123", "Robin Archer", "Recurve", "Outdoor", 500, new DateOnly(2026, 1, 31)) };

        var bytes = new PersonalBestExcelService().Export(config, rows, new DateOnly(2026, 2, 1));

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Export");
        Assert.Equal("Datum", sheet.Cell(1, 1).GetString());
        Assert.Equal("Bondsnummer", sheet.Cell(1, 2).GetString());
        Assert.Equal("31-01-2026", sheet.Cell(2, 1).GetString());
        Assert.Equal("123", sheet.Cell(2, 2).GetString());
    }
}
