using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class LegacyExcelReader
{
    private static readonly string[] RelevantHeaders = ["Fornitore", "Data", "Carico", "Numero Etichetta", "Pezzi", "A", "B", "C", "Finito il", "Qualità", "B reale", "Tipo", "Metri Lineari", "Metri Cubi", "Metri Cubi Stimati", "Classificato", "Data classificazione", "qr", "Anno"];

    public LegacyWorkbookData Read(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("File Excel non trovato.", filePath);
        if (!string.Equals(Path.GetExtension(filePath), ".xlsm", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Selezionare un file Excel .xlsm.");
        using var document = SpreadsheetDocument.Open(filePath, false);
        var workbook = document.WorkbookPart ?? throw new InvalidOperationException("Cartella Excel non valida.");
        var warehouse = GetSheet(workbook, "Magazzino");
        var warehouseRows = warehouse.Worksheet.GetFirstChild<SheetData>()?.Elements<Row>().ToArray() ?? [];
        var (headerRow, columns) = FindHeaders(workbook, warehouseRows, "Magazzino", true);
        var rows = ReadWarehouseRows(workbook, warehouseRows, headerRow, columns);
        var identifiers = new List<string>(); var availableCount = 0;
        var available = TryGetSheet(workbook, "Materiale Disponibile");
        if (available is not null)
        {
            var availableRows = available.Worksheet.GetFirstChild<SheetData>()?.Elements<Row>().ToArray() ?? [];
            var (availableHeader, availableColumns) = FindHeaders(workbook, availableRows, "Materiale Disponibile", false);
            foreach (var row in availableRows.Where(x => (x.RowIndex?.Value ?? 0) > availableHeader))
            {
                if (IsBlankRow(workbook, row)) continue;
                availableCount++;
                var excelRow = (int)(row.RowIndex?.Value ?? 0);
                identifiers.Add(BuildIdentifier(Text(workbook, row, availableColumns, "qr"), Text(workbook, row, availableColumns, "Numero Etichetta"), Text(workbook, row, availableColumns, "Carico"), Text(workbook, row, availableColumns, "Fornitore"), Text(workbook, row, availableColumns, "Pezzi"), Text(workbook, row, availableColumns, "A"), Text(workbook, row, availableColumns, "B"), Text(workbook, row, availableColumns, "C"), excelRow));
            }
        }
        return new LegacyWorkbookData { FilePath = filePath, FoundColumns = columns.Keys.OrderBy(x => x).ToArray(), WarehouseRows = rows, AvailableIdentifiers = identifiers, AvailableSheetRows = availableCount };
    }

    private static List<LegacyStagingRow> ReadWarehouseRows(WorkbookPart workbook, Row[] source, int headerRow, Dictionary<string, int> columns)
    {
        var result = new List<LegacyStagingRow>();
        foreach (var row in source.Where(x => (x.RowIndex?.Value ?? 0) > headerRow))
        {
            if (IsBlankRow(workbook, row)) continue;
            var excelRow = (int)(row.RowIndex?.Value ?? 0);
            result.Add(new LegacyStagingRow
            {
                ExcelRow = excelRow, SupplierOriginal = NullIfBlank(Text(workbook, row, columns, "Fornitore")), Date = Date(workbook, row, columns, "Data"), LoadNumber = NullIfBlank(Text(workbook, row, columns, "Carico")),
                PackageLabel = NullIfBlank(Text(workbook, row, columns, "Numero Etichetta")), Pieces = Decimal(workbook, row, columns, "Pezzi"), InputWidth = Decimal(workbook, row, columns, "A"), InputThickness = Decimal(workbook, row, columns, "B"),
                InputLength = Decimal(workbook, row, columns, "C"), FinishedRawValue = NullIfBlank(Text(workbook, row, columns, "Finito il")), FinishedOn = Date(workbook, row, columns, "Finito il"), QualityOriginal = NullIfBlank(Text(workbook, row, columns, "Qualità")), RealWidth = Decimal(workbook, row, columns, "B reale"),
                Certification = NullIfBlank(Text(workbook, row, columns, "Tipo")), LinearMeters = Decimal(workbook, row, columns, "Metri Lineari"), ExcelCubicMeters = Decimal(workbook, row, columns, "Metri Cubi"),
                LegacyEstimatedCubicMeters = Decimal(workbook, row, columns, "Metri Cubi Stimati"), ClassifiedOriginal = NullIfBlank(Text(workbook, row, columns, "Classificato")), ClassificationDate = Date(workbook, row, columns, "Data classificazione"),
                Qr = NullIfBlank(Text(workbook, row, columns, "qr")), Year = NullIfBlank(Text(workbook, row, columns, "Anno"))
            });
        }
        return result;
    }

    private static (int Row, Dictionary<string, int> Columns) FindHeaders(WorkbookPart workbook, Row[] rows, string sheetName, bool requireFinished)
    {
        var bestRow = 0; var best = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Take(40))
        {
            var found = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in row.Elements<Cell>())
            {
                var normalized = NormalizeHeader(CellText(workbook, cell));
                var canonical = RelevantHeaders.FirstOrDefault(x => NormalizeHeader(x) == normalized);
                if (canonical is not null && !found.ContainsKey(canonical)) found[canonical] = ColumnIndex(cell.CellReference?.Value);
            }
            if (found.Count > best.Count) { bestRow = (int)(row.RowIndex?.Value ?? 0); best = found; }
        }
        if (bestRow == 0 || !best.ContainsKey("Fornitore") || (requireFinished && !best.ContainsKey("Finito il"))) throw new InvalidOperationException($"Intestazioni non riconosciute nel foglio '{sheetName}'.");
        return (bestRow, best);
    }

    private static WorksheetPart GetSheet(WorkbookPart workbook, string name) => TryGetSheet(workbook, name) ?? throw new InvalidOperationException($"Il foglio obbligatorio '{name}' non è presente.");
    private static WorksheetPart? TryGetSheet(WorkbookPart workbook, string name)
    {
        var sheet = workbook.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault(x => string.Equals(x.Name?.Value, name, StringComparison.OrdinalIgnoreCase));
        return sheet?.Id?.Value is { } id ? workbook.GetPartById(id) as WorksheetPart : null;
    }
    private static string Text(WorkbookPart workbook, Row row, Dictionary<string, int> columns, string name)
    {
        if (!columns.TryGetValue(name, out var column)) return "";
        var cell = row.Elements<Cell>().FirstOrDefault(x => ColumnIndex(x.CellReference?.Value) == column);
        return cell is null ? "" : CellText(workbook, cell).Trim();
    }
    private static string CellText(WorkbookPart workbook, Cell cell)
    {
        if (cell.DataType?.Value == CellValues.InlineString) return cell.InlineString?.InnerText ?? "";
        var value = cell.CellValue?.InnerText ?? "";
        if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(value, out var index)) return workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().ElementAtOrDefault(index)?.InnerText ?? value;
        if (cell.DataType?.Value == CellValues.Boolean) return value == "1" ? "Sì" : "No";
        return value;
    }
    private static decimal? Decimal(WorkbookPart workbook, Row row, Dictionary<string, int> columns, string name)
    {
        var text = Text(workbook, row, columns, name).Replace(" ", "");
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var number) || decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("it-IT"), out number) ? number : null;
    }
    private static DateTime? Date(WorkbookPart workbook, Row row, Dictionary<string, int> columns, string name)
    {
        var text = Text(workbook, row, columns, name);
        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial)) try { return DateTime.FromOADate(serial); } catch (ArgumentException) { }
        var compactDate = Regex.Match(text, @"^(?<day>\d{2})(?<month>\d{2})/(?<year>\d{4})$");
        if (compactDate.Success && int.TryParse(compactDate.Groups["day"].Value, out var day) && int.TryParse(compactDate.Groups["month"].Value, out var month) && int.TryParse(compactDate.Groups["year"].Value, out var year))
            try { return new DateTime(year, month, day); } catch (ArgumentOutOfRangeException) { return null; }
        return DateTime.TryParse(text, CultureInfo.GetCultureInfo("it-IT"), DateTimeStyles.None, out var date) ? date : null;
    }
    private static int ColumnIndex(string? reference) { var index = 0; foreach (var c in reference?.TakeWhile(char.IsLetter) ?? []) index = index * 26 + char.ToUpperInvariant(c) - 'A' + 1; return index; }
    private static string NormalizeHeader(string value) { var decomposed = value.Trim().Normalize(NormalizationForm.FormD); return new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c)).Select(char.ToLowerInvariant).ToArray()); }
    private static bool IsBlankRow(WorkbookPart workbook, Row row) => !row.Elements<Cell>().Any(x => !string.IsNullOrWhiteSpace(CellText(workbook, x)));
    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    public static string BuildIdentifier(string? qr, string? label, string? load, string? supplier, string? pieces, string? width, string? thickness, string? length, int row)
    {
        static string N(string? value) => value?.Trim().ToUpperInvariant() ?? "";
        if (!string.IsNullOrWhiteSpace(label)) return "E:" + N(load) + "|" + Regex.Replace(N(label), @"^PACCO\s+", "");
        if (!string.IsNullOrWhiteSpace(qr)) return "Q:" + N(qr);
        var composite = string.Join('|', N(load), N(supplier), N(pieces), N(width), N(thickness), N(length));
        return composite.Replace("|", "").Length > 0 ? "D:" + composite : "R:" + row;
    }
}
