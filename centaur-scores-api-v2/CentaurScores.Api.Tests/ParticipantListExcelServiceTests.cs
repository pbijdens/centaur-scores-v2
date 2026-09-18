using ClosedXML.Excel;
using CentaurScores.Api.Application;
using CentaurScores.Api.Domain;

namespace CentaurScores.Api.Tests;

public sealed class ParticipantListExcelServiceTests
{
    private static Stream BuildWorkbook(string categoryHeader, params (string Number, string Name, string LastName, string Active, string Category)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Data");
        sheet.Cell(1, 1).Value = "Number";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(1, 3).Value = "Last name";
        sheet.Cell(1, 4).Value = "Active";
        sheet.Cell(1, 5).Value = categoryHeader;
        for (var row = 0; row < rows.Length; row++)
        {
            var (number, name, lastName, active, category) = rows[row];
            var excelRow = row + 2;
            sheet.Cell(excelRow, 1).Value = number;
            sheet.Cell(excelRow, 2).Value = name;
            sheet.Cell(excelRow, 3).Value = lastName;
            sheet.Cell(excelRow, 4).Value = active;
            sheet.Cell(excelRow, 5).Value = category;
        }
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Import_matches_a_category_value_literally_named_the_unknown_label()
    {
        var tenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var category = new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Name = "Klasse",
            Values =
            [
                new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = categoryId, ValueId = 1, Name = "Senior" },
                new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = categoryId, ValueId = 2, Name = "Unknown" }
            ]
        };
        using var stream = BuildWorkbook("Klasse", ("1", "Amy Archer", "Archer", "TRUE", "Unknown"));

        var result = new ParticipantListExcelService().Import(stream, [category]);

        var row = Assert.Single(result.Rows);
        Assert.Equal(2, row.Categories[categoryId]);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Import_treats_the_unknown_label_as_absent_when_no_category_value_matches_it()
    {
        var tenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var category = new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Name = "Klasse",
            Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = categoryId, ValueId = 1, Name = "Senior" }]
        };
        using var stream = BuildWorkbook("Klasse", ("1", "Amy Archer", "Archer", "TRUE", "Unknown"));

        var result = new ParticipantListExcelService().Import(stream, [category]);

        var row = Assert.Single(result.Rows);
        Assert.Empty(row.Categories);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Import_warns_and_treats_the_value_as_absent_when_the_text_matches_no_known_value()
    {
        var tenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var category = new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Name = "Klasse",
            Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = categoryId, ValueId = 1, Name = "Senior" }]
        };
        using var stream = BuildWorkbook("Klasse", ("1", "Amy Archer", "Archer", "TRUE", "Nonsense"));

        var result = new ParticipantListExcelService().Import(stream, [category]);

        var row = Assert.Single(result.Rows);
        Assert.Empty(row.Categories);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("Nonsense", warning);
        Assert.Contains("Klasse", warning);
    }

    [Fact]
    public void Import_treats_a_blank_category_cell_as_absent_without_a_warning()
    {
        var tenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var category = new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Name = "Klasse",
            Values = [new CategoryValue { Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = categoryId, ValueId = 1, Name = "Senior" }]
        };
        using var stream = BuildWorkbook("Klasse", ("1", "Amy Archer", "Archer", "TRUE", ""));

        var result = new ParticipantListExcelService().Import(stream, [category]);

        var row = Assert.Single(result.Rows);
        Assert.Empty(row.Categories);
        Assert.Empty(result.Warnings);
    }
}
