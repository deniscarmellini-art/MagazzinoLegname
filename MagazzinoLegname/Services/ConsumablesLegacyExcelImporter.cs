using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class ConsumablesLegacyExcelImporter
{
    private const string LegacyOrigin = "LegacyExcelImport";

    public ConsumablesLegacyImportPlan Analyze(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("File Excel non trovato.", filePath);
        var fingerprint = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(filePath)));
        using var document = SpreadsheetDocument.Open(filePath, false);
        var workbook = document.WorkbookPart ?? throw new InvalidOperationException("Cartella Excel non valida.");
        var productsPart = Sheet(workbook, "PRODOTTI") ?? throw new InvalidOperationException("Il foglio PRODOTTI non è presente.");
        var rows = productsPart.Worksheet.GetFirstChild<SheetData>()?.Elements<Row>().ToArray() ?? [];
        var header = rows.Take(40).OrderByDescending(row => row.Elements<Cell>().Count(cell => Normalize(CellText(workbook, cell)).Contains("prodotto") || Normalize(CellText(workbook, cell)).StartsWith("stock"))).FirstOrDefault()
            ?? throw new InvalidOperationException("Intestazioni del foglio PRODOTTI non riconosciute.");
        var headers = header.Elements<Cell>().ToDictionary(cell => ColumnIndex(cell.CellReference?.Value), cell => CellText(workbook, cell).Trim());
        var productColumn = Find(headers, "prodotto") ?? throw new InvalidOperationException("Colonna Prodotto non trovata.");
        var supplierColumn = Find(headers, "fornitore"); var departmentColumn = Find(headers, "utilizzo", "reparto");
        var unitColumn = Find(headers, "udm", "unitamisura"); var quantityPerUnitColumn = Find(headers, "qtaperudm", "quantitaperudm"); var minimumColumn = Find(headers, "stockminimosicurezza", "scortaminima");
        var consumptionColumn = Find(headers, "consumomedio"); var leadColumn = Find(headers, "leadtime", "leadtimeconsegna");
        var notesColumn = Find(headers, "notepackaging", "packaging", "note"); var orderedColumn = Find(headers, "inordine");
        var extraColumn = headers.Keys.DefaultIfEmpty().Max() + 1;
        var stockColumns = headers.Select(pair => (Column: pair.Key, Date: StockDate(pair.Value), Header: pair.Value)).Where(item => item.Date.HasValue).OrderBy(item => item.Date).ToArray();
        var store = ConsumablesStore.Shared; var candidates = new List<ConsumableItem>(); var readings = new List<ConsumableInventoryReading>();
        var orders = new List<ConsumableOrderInfo>(); var warnings = new List<string>();
        var usedCodes = store.Items.Select(item => item.InternalCode).ToHashSet(StringComparer.OrdinalIgnoreCase); var nextCode = NextCodeNumber(usedCodes);
        var newItems = 0; var existingItems = 0; var ambiguousItems = 0;

        foreach (var row in rows.Where(row => (row.RowIndex?.Value ?? 0) > (header.RowIndex?.Value ?? 0)))
        {
            var product = Text(workbook, row, productColumn); if (string.IsNullOrWhiteSpace(product)) continue;
            var excelRow = row.RowIndex?.Value ?? 0; var supplier = Text(workbook, row, supplierColumn); var department = Text(workbook, row, departmentColumn);
            var unit = NormalizeUnit(Text(workbook, row, unitColumn)); var matches = MatchExisting(store.Items, product, supplier, department, unit).ToArray();
            if (matches.Length > 1) { ambiguousItems++; warnings.Add($"Riga {excelRow}: match ambiguo per '{product}'. La conferma è bloccata finché l'anagrafica non viene resa univoca."); }
            var existing = matches.Length == 1 ? matches[0] : null; var quantityPerUnitText = Text(workbook, row, quantityPerUnitColumn); var minimumText = Text(workbook, row, minimumColumn);
            var consumption = Text(workbook, row, consumptionColumn); var lead = Text(workbook, row, leadColumn);
            var packaging = Text(workbook, row, notesColumn); var extraNote = Text(workbook, row, extraColumn); var orderedText = Text(workbook, row, orderedColumn);
            var quantityPerUnit = TryDecimal(quantityPerUnitText, out var parsedQuantityPerUnit) && parsedQuantityPerUnit > 0 ? parsedQuantityPerUnit : (decimal?)null;
            var minimum = TryMinimumStock(minimumText, unit, out var parsedMinimum, out var normalizedMinimum) ? parsedMinimum : (decimal?)null;
            if (normalizedMinimum) warnings.Add($"Riga {excelRow}: scorta minima '{minimumText}' normalizzata in {parsedMinimum:N2} {unit}.");
            if (!string.IsNullOrWhiteSpace(minimumText) && !minimum.HasValue) warnings.Add($"Riga {excelRow}: scorta minima ambigua '{minimumText}', valore non importato.");
            var needsVerification = string.IsNullOrWhiteSpace(supplier) || string.IsNullOrWhiteSpace(unit) || !quantityPerUnit.HasValue || !minimum.HasValue || string.IsNullOrWhiteSpace(consumption) || string.IsNullOrWhiteSpace(lead) || matches.Length > 1;
            var item = new ConsumableItem
            {
                Id = existing?.Id ?? Guid.NewGuid(), InternalCode = existing?.InternalCode ?? NextCode(usedCodes, ref nextCode), ProductName = product.Trim(),
                SupplierName = supplier.Trim(), Department = department.Trim(), LegacySupplierName = supplier.Trim(), LegacyDepartment = department.Trim(),
                UnitOfMeasure = unit, QuantityPerUnit = quantityPerUnit, MinimumStock = minimum, ConsumptionAverageText = consumption.Trim(), LeadTimeText = lead.Trim(), LeadTimeDays = Integer(lead),
                Packaging = packaging.Trim(), Notes = extraNote.Trim(), LegacyExtraNote = extraNote.Trim(), LegacyOrderText = orderedText.Trim(),
                NeedsVerification = needsVerification, PhotoPath = existing?.PhotoPath, IsActive = existing?.IsActive ?? true
            };
            candidates.Add(item); if (existing is null) newItems++; else existingItems++;
            if (needsVerification) warnings.Add($"Riga {excelRow}: '{product}' contiene dati mancanti o non strutturati; sarà Da verificare.");
            foreach (var stock in stockColumns)
            {
                var cell = Cell(row, stock.Column); if (cell is null || IsTrulyBlank(cell)) continue; var raw = CellText(workbook, cell).Trim();
                if (!TryDecimal(raw, out var quantity)) { warnings.Add($"Riga {excelRow}, {stock.Header}: valore stock non numerico '{raw}'."); continue; }
                readings.Add(new ConsumableInventoryReading { MaterialId = item.Id, ReadingDate = stock.Date!.Value, Quantity = quantity, Operator = null, Origin = LegacyOrigin, Note = $"Importazione legacy · {Path.GetFileName(filePath)}" });
            }
            if (!string.IsNullOrWhiteSpace(orderedText) && !orderedText.Equals("no", StringComparison.OrdinalIgnoreCase) && orderedText != "0")
                orders.Add(new ConsumableOrderInfo { MaterialId = item.Id, Note = orderedText.Trim(), Status = ConsumableOrderStatus.None });
        }
        var photos = ExtractPhotos(workbook, candidates, warnings);
        if (store.HasImported(fingerprint)) warnings.Insert(0, "Questo identico file è già stato importato: una nuova conferma non creerà duplicati.");
        return new ConsumablesLegacyImportPlan(candidates, readings, orders, warnings, photos.Count, photos.Count(photo => photo.Status == ConsumableLegacyPhotoStatus.Associated),
            fingerprint, stockColumns.Select(item => item.Date!.Value).Distinct().ToArray(), photos, newItems, existingItems, candidates.Count(item => item.NeedsVerification), ambiguousItems);
    }

    public ConsumablesLegacyImportResult Commit(ConsumablesLegacyImportPlan plan)
    {
        if (!plan.CanImport) throw new InvalidOperationException("Il piano contiene associazioni ambigue o nessun articolo importabile.");
        var store = ConsumablesStore.Shared; if (store.HasImported(plan.FileFingerprint)) return new ConsumablesLegacyImportResult(0, 0, 0);
        var newItems = plan.Items.Where(candidate => store.Items.All(existing => existing.Id != candidate.Id)).ToArray();
        var knownIds = store.Items.Select(item => item.Id).Concat(newItems.Select(item => item.Id)).ToHashSet();
        var readings = plan.Readings.Where(reading => knownIds.Contains(reading.MaterialId) && !store.Readings.Any(existing => existing.MaterialId == reading.MaterialId && existing.ReadingDate.Date == reading.ReadingDate.Date && existing.Origin == LegacyOrigin)).ToArray();
        var orders = plan.Orders.Where(order => newItems.Any(item => item.Id == order.MaterialId) && store.Orders.All(existing => existing.MaterialId != order.MaterialId)).ToArray();
        store.Import(newItems, readings, orders);
        var photoDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MagazzinoLegname", "Consumables", "Photos"); Directory.CreateDirectory(photoDirectory);
        var savedPhotos = 0; var orphanDirectory = Path.Combine(photoDirectory, $"DaAssociare_{plan.FileFingerprint[..Math.Min(12, plan.FileFingerprint.Length)]}");
        foreach (var photo in plan.Photos ?? [])
        {
            var item = photo.MaterialId.HasValue ? store.Items.FirstOrDefault(candidate => candidate.Id == photo.MaterialId) : null;
            if (item is not null && string.IsNullOrWhiteSpace(item.PhotoPath))
            {
                var path = Path.Combine(photoDirectory, $"{item.InternalCode}_{Guid.NewGuid():N}{photo.Extension}"); File.WriteAllBytes(path, photo.Content); item.PhotoPath = path; savedPhotos++;
            }
            else if (photo.Status != ConsumableLegacyPhotoStatus.Associated)
            {
                Directory.CreateDirectory(orphanDirectory); File.WriteAllBytes(Path.Combine(orphanDirectory, photo.FileName), photo.Content);
            }
        }
        store.MarkImported(plan.FileFingerprint); store.NotifyChanged();
        return new ConsumablesLegacyImportResult(newItems.Length, readings.Length, orders.Length, savedPhotos, plan.UnassociatedImages,
            readings.Select(item => (DateTime?)item.ReadingDate).DefaultIfEmpty().Min(), readings.Select(item => (DateTime?)item.ReadingDate).DefaultIfEmpty().Max(), newItems.Count(item => item.NeedsVerification));
    }

    private static IReadOnlyList<ConsumableLegacyPhoto> ExtractPhotos(WorkbookPart workbook, IReadOnlyList<ConsumableItem> items, List<string> warnings)
    {
        var sheet = Sheet(workbook, "FOTO"); if (sheet?.DrawingsPart is null) return [];
        var descriptions = sheet.Worksheet.GetFirstChild<SheetData>()?.Descendants<Cell>().Select(cell => CellText(workbook, cell).Trim()).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray() ?? [];
        var result = new List<ConsumableLegacyPhoto>(); var imageParts = sheet.DrawingsPart.ImageParts.ToArray();
        for (var index = 0; index < imageParts.Length; index++)
        {
            var part = imageParts[index]; var description = index < descriptions.Length ? descriptions[index] : $"Immagine {index + 1}";
            using var stream = part.GetStream(); using var memory = new MemoryStream(); stream.CopyTo(memory);
            var ranked = items.Select(item => (Item: item, Score: MatchScore(description, item.ProductName))).OrderByDescending(item => item.Score).ToArray();
            var best = ranked.FirstOrDefault(); var second = ranked.Skip(1).FirstOrDefault();
            var status = best.Item is not null && best.Score >= .62 && best.Score - second.Score >= .18 ? ConsumableLegacyPhotoStatus.Associated : best.Item is not null && best.Score >= .35 ? ConsumableLegacyPhotoStatus.Uncertain : ConsumableLegacyPhotoStatus.Unassociated;
            var extension = part.ContentType.ToLowerInvariant() switch { "image/png" => ".png", "image/jpeg" => ".jpg", "image/bmp" => ".bmp", _ => ".bin" };
            result.Add(new ConsumableLegacyPhoto($"foto-{index + 1}{extension}", description, memory.ToArray(), extension, status == ConsumableLegacyPhotoStatus.Associated ? best.Item?.Id : null,
                status == ConsumableLegacyPhotoStatus.Associated ? best.Item?.ProductName : null, status, status switch { ConsumableLegacyPhotoStatus.Associated => $"Associata a {best.Item!.ProductName}", ConsumableLegacyPhotoStatus.Uncertain => $"Possibile corrispondenza: {best.Item!.ProductName}", _ => "Nessuna corrispondenza affidabile" }));
        }
        if (result.Any(photo => photo.Status != ConsumableLegacyPhotoStatus.Associated)) warnings.Add($"{result.Count(photo => photo.Status != ConsumableLegacyPhotoStatus.Associated)} immagini richiedono associazione manuale; restano disponibili nell'anteprima.");
        return result;
    }

    private static IEnumerable<ConsumableItem> MatchExisting(IEnumerable<ConsumableItem> items, string product, string supplier, string department, string unit)
    { var matches = items.Where(item => Normalize(item.ProductName) == Normalize(product)).ToArray(); if (matches.Length <= 1) return matches; var supported = matches.Where(item => SameOrEmpty(item.SupplierName, supplier) && SameOrEmpty(item.Department, department) && SameOrEmpty(item.UnitOfMeasure, unit)).ToArray(); return supported.Length > 0 ? supported : matches; }
    private static bool SameOrEmpty(string left, string right) => string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right) || Normalize(left) == Normalize(right);
    private static int NextCodeNumber(IEnumerable<string> codes) => codes.Where(code => code.StartsWith("CON-", StringComparison.OrdinalIgnoreCase)).Select(code => int.TryParse(code.AsSpan(4), out var value) ? value : 0).DefaultIfEmpty().Max() + 1;
    private static string NextCode(HashSet<string> used, ref int number) { string code; do code = $"CON-{number++:000}"; while (!used.Add(code)); return code; }
    private static double MatchScore(string left, string right) { var a = Tokens(left); var b = Tokens(right); return a.Count == 0 || b.Count == 0 ? 0 : (double)a.Intersect(b).Count() / a.Union(b).Count(); }
    private static HashSet<string> Tokens(string value) => Regex.Split(NormalizeWords(value), @"\s+").Where(token => token.Length >= 3 && token is not "per" and not "della" and not "degli").ToHashSet();
    private static string NormalizeWords(string value) => Regex.Replace(RemoveDiacritics(value).ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
    private static string NormalizeUnit(string value) => value.Trim().ToLowerInvariant() switch { "l" or "lt" or "litri" or "litro" => "L", "kg" or "kgs" => "kg", "nr" or "n" or "pz" => "nr", "scatola" or "scatole" => "scatole", "foglio" or "fogli" => "fogli", _ => value.Trim() };
    private static WorksheetPart? Sheet(WorkbookPart workbook, string name) { var sheet = workbook.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault(item => string.Equals(item.Name?.Value, name, StringComparison.OrdinalIgnoreCase)); return sheet?.Id?.Value is { } id ? workbook.GetPartById(id) as WorksheetPart : null; }
    private static int? Find(Dictionary<int, string> headers, params string[] names) { foreach (var pair in headers) if (names.Any(name => Normalize(pair.Value).Contains(name))) return pair.Key; return null; }
    private static Cell? Cell(Row row, int column) => row.Elements<Cell>().FirstOrDefault(item => ColumnIndex(item.CellReference?.Value) == column);
    private static string Text(WorkbookPart workbook, Row row, int? column) => column.HasValue && Cell(row, column.Value) is { } cell ? CellText(workbook, cell).Trim() : string.Empty;
    private static bool IsTrulyBlank(Cell cell) => cell.CellValue is null && cell.InlineString is null;
    private static string CellText(WorkbookPart workbook, Cell cell) { if (cell.DataType?.Value == CellValues.InlineString) return cell.InlineString?.InnerText ?? ""; var value = cell.CellValue?.InnerText ?? ""; return cell.DataType?.Value == CellValues.SharedString && int.TryParse(value, out var index) ? workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().ElementAtOrDefault(index)?.InnerText ?? value : value; }
    private static int? Integer(string text) { var match = Regex.Match(text, @"\d+"); return match.Success && int.TryParse(match.Value, out var value) ? value : null; }
    private static bool TryDecimal(string text, out decimal value) => decimal.TryParse(text.Replace(" ", ""), NumberStyles.Any, CultureInfo.GetCultureInfo("it-IT"), out value) || decimal.TryParse(text.Replace(" ", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    private static bool TryMinimumStock(string text, string unit, out decimal value, out bool normalized)
    {
        normalized = false;
        if (TryDecimal(text, out value)) return true;
        var match = Regex.Match(text, @"^\s*(?<number>\d+(?:[.,]\d+)?)\s*(?<unit>[\p{L}]+)\s*$", RegexOptions.CultureInvariant);
        if (!match.Success || !TryDecimal(match.Groups["number"].Value, out value)) return false;
        var suffix = NormalizeUnit(match.Groups["unit"].Value);
        if (string.IsNullOrWhiteSpace(unit) || !suffix.Equals(NormalizeUnit(unit), StringComparison.OrdinalIgnoreCase)) return false;
        normalized = true; return true;
    }
    private static DateTime? StockDate(string header) { if (!Normalize(header).StartsWith("stock")) return null; var match = Regex.Match(header, @"(?<d>\d{1,2})[./-](?<m>\d{1,2})[./-](?<y>\d{2,4})"); if (!match.Success) return null; var year = int.Parse(match.Groups["y"].Value); if (year < 100) year += 2000; return DateTime.TryParseExact($"{match.Groups["d"].Value}/{match.Groups["m"].Value}/{year}", "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null; }
    private static int ColumnIndex(string? reference) { var value = 0; foreach (var c in reference?.TakeWhile(char.IsLetter) ?? []) value = value * 26 + char.ToUpperInvariant(c) - 'A' + 1; return value; }
    private static string Normalize(string value) => new(RemoveDiacritics(value).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static string RemoveDiacritics(string value) { var decomposed = value.Trim().Normalize(NormalizationForm.FormD); return new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()); }
}
