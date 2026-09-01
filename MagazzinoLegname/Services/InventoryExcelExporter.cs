using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class InventoryExcelExporter
{
    private const uint HeaderStyle = 1;
    private const uint DateStyle = 2;
    private const uint DecimalStyle = 3;
    private const uint CurrencyStyle = 4;

    public void Export(IReadOnlyCollection<InventoryPackage> packages, string filePath, DateTime exportedAt)
    {
        using var document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        AddStyles(workbookPart);

        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        var inventoryPart = workbookPart.AddNewPart<WorksheetPart>();
        inventoryPart.Worksheet = BuildInventoryWorksheet(packages);
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(inventoryPart), SheetId = 1, Name = "Giacenze" });

        var summaryPart = workbookPart.AddNewPart<WorksheetPart>();
        summaryPart.Worksheet = BuildSummaryWorksheet(packages, exportedAt);
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(summaryPart), SheetId = 2, Name = "Riepilogo" });

        workbookPart.Workbook.Save();
    }

    private static Worksheet BuildInventoryWorksheet(IReadOnlyCollection<InventoryPackage> packages)
    {
        var sheetData = new SheetData();
        sheetData.Append(BuildHeaderRow([
            "Codice pacco", "Tipo pacco", "Carico", "Fornitore", "Data entrata", "Pacco X/Y", "Pezzi",
            "Spessore ingresso", "Famiglia convenzionale", "Larghezza ingresso", "Larghezza dopo prepiallatura", "Lunghezza",
            "Qualità", "Stato classificazione", "Rettifica scarti", "MC ingresso", "MC giacenza", "Valore €"
        ]));

        uint rowIndex = 2;
        foreach (var package in packages)
        {
            var row = new Row { RowIndex = rowIndex };
            row.Append(
                TextCell(package.PackageCode),
                TextCell(package.PackageTypeDisplay),
                TextCell(package.LoadNumber),
                TextCell(package.SupplierName),
                DateCell(package.ArrivalDate),
                TextCell(package.PackagePosition),
                package.PieceCount.HasValue ? NumberCell(package.PieceCount.Value, DecimalStyle) : TextCell(string.Empty),
                NumberCell(package.IncomingThickness, DecimalStyle),
                NumberCell(package.ConventionalThickness, DecimalStyle),
                NumberCell(package.IncomingWidth, DecimalStyle),
                NumberCell(package.WidthAfterPlaning, DecimalStyle),
                NumberCell(package.IncomingLength, DecimalStyle),
                TextCell(package.Quality),
                TextCell(package.ClassificationStatus),
                TextCell(package.IsSupplementary ? "—" : package.UsesRealCubicMeters ? "Rettificato" : "Da rettificare"),
                package.IsSupplementary ? TextCell(string.Empty) : NumberCell(package.IncomingCubicMeters, DecimalStyle),
                package.IsSupplementary ? TextCell(string.Empty) : NumberCell(package.InventoryCubicMeters, DecimalStyle),
                package.PackageValue.HasValue ? NumberCell(package.PackageValue.Value, CurrencyStyle) : TextCell(package.IsSupplementary ? "—" : "N/D"));
            sheetData.Append(row);
            rowIndex++;
        }

        var lastRow = Math.Max(1, packages.Count + 1);
        return new Worksheet(
            FreezeFirstRow(),
            InventoryColumns(),
            sheetData,
            new AutoFilter { Reference = $"A1:R{lastRow}" });
    }

    private static Worksheet BuildSummaryWorksheet(IReadOnlyCollection<InventoryPackage> packages, DateTime exportedAt)
    {
        var sheetData = new SheetData();
        sheetData.Append(BuildHeaderRow(["Voce", "Valore"]));

        var accountedPackages = packages.Where(package => package.IsAccountedPackage).ToArray();
        var knownValues = accountedPackages.Where(package => package.PackageValue.HasValue).ToArray();
        var missingValueCount = accountedPackages.Length - knownValues.Length;
        var totalValue = knownValues.Sum(package => package.PackageValue!.Value);
        var valueDisplay = accountedPackages.Length == 0 || knownValues.Length == 0
            ? "N/D"
            : missingValueCount > 0
                ? $"{totalValue:N2} € - parziale, {missingValueCount:N0} pacchi N/D"
                : $"{totalValue:N2} €";

        AppendSummaryRow(sheetData, "Numero pacchi esportati", packages.Count.ToString("N0", CultureInfo.CurrentCulture));
        AppendSummaryRow(sheetData, "MC ingresso totali", accountedPackages.Sum(package => package.IncomingCubicMeters).ToString("N2", CultureInfo.CurrentCulture));
        AppendSummaryRow(sheetData, "MC giacenza totali", accountedPackages.Sum(package => package.InventoryCubicMeters).ToString("N2", CultureInfo.CurrentCulture));
        AppendSummaryRow(sheetData, "Valore totale disponibile", valueDisplay);
        AppendSummaryRow(sheetData, "Data/ora esportazione", exportedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture));

        return new Worksheet(FreezeFirstRow(), SummaryColumns(), sheetData);
    }

    private static Row BuildHeaderRow(string[] headers)
    {
        var row = new Row { RowIndex = 1 };
        foreach (var header in headers) row.Append(TextCell(header, HeaderStyle));
        return row;
    }

    private static void AppendSummaryRow(SheetData sheetData, string label, string value)
    {
        var row = new Row { RowIndex = (uint)sheetData.ChildElements.Count + 1 };
        row.Append(TextCell(label), TextCell(value));
        sheetData.Append(row);
    }

    private static SheetViews FreezeFirstRow() => new(new SheetView
    {
        WorkbookViewId = 0,
        Pane = new Pane { VerticalSplit = 1D, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen }
    });

    private static Columns InventoryColumns() => new(
        Column(1, 1, 18), Column(2, 2, 16), Column(3, 3, 12), Column(4, 4, 24),
        Column(5, 7, 12), Column(8, 12, 17), Column(13, 13, 12), Column(14, 15, 22),
        Column(16, 17, 13), Column(18, 18, 14));

    private static Columns SummaryColumns() => new(Column(1, 1, 28), Column(2, 2, 34));

    private static Column Column(uint min, uint max, double width) => new()
    {
        Min = min,
        Max = max,
        Width = width,
        CustomWidth = true
    };

    private static Cell TextCell(string? value, uint styleIndex = 0) => new()
    {
        DataType = CellValues.InlineString,
        StyleIndex = styleIndex,
        InlineString = new InlineString(new Text(value ?? string.Empty))
    };

    private static Cell DateCell(DateTime value) => new()
    {
        CellValue = new CellValue(value.ToOADate().ToString(CultureInfo.InvariantCulture)),
        DataType = CellValues.Number,
        StyleIndex = DateStyle
    };

    private static Cell NumberCell(decimal value, uint styleIndex) => new()
    {
        CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture)),
        DataType = CellValues.Number,
        StyleIndex = styleIndex
    };

    private static void AddStyles(WorkbookPart workbookPart)
    {
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = new Stylesheet(
            new NumberingFormats(
                new NumberingFormat { NumberFormatId = 164, FormatCode = "dd/mm/yyyy" },
                new NumberingFormat { NumberFormatId = 165, FormatCode = "#,##0.00" },
                new NumberingFormat { NumberFormatId = 166, FormatCode = "#,##0.00 \u20ac" }) { Count = 3 },
            new Fonts(new Font(), new Font(new Bold())) { Count = 2 },
            new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 })) { Count = 2 },
            new Borders(new Border()) { Count = 1 },
            new CellFormats(
                new CellFormat(),
                new CellFormat { FontId = 1, ApplyFont = true },
                new CellFormat { NumberFormatId = 164, ApplyNumberFormat = true },
                new CellFormat { NumberFormatId = 165, ApplyNumberFormat = true },
                new CellFormat { NumberFormatId = 166, ApplyNumberFormat = true }) { Count = 5 });
        stylesPart.Stylesheet.Save();
    }
}
