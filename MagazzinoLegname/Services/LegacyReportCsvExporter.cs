using System.Globalization;
using System.IO;
using System.Text;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class LegacyReportCsvExporter
{
    public IReadOnlyList<string> Export(LegacyImportReport report, string selectedPath)
    {
        var directory = Path.GetDirectoryName(selectedPath) ?? throw new InvalidOperationException("Percorso di esportazione non valido.");
        var prefix = Path.GetFileNameWithoutExtension(selectedPath);
        var paths = new[]
        {
            Path.Combine(directory, prefix + "_righe_escluse.csv"), Path.Combine(directory, prefix + "_anomalie.csv"),
            Path.Combine(directory, prefix + "_fornitori_dubbi.csv"), Path.Combine(directory, prefix + "_qualita_legacy.csv")
        };
        Write(paths[0], ["Riga Excel", "Fornitore", "Data", "Carico", "Numero Etichetta", "Pezzi", "A", "B", "C", "Qualità", "Finito il", "Motivo esclusione", "Gravità"], report.ExcludedRowDetails.Select(x => new[] { x.ExcelRow.ToString(), x.Supplier, Date(x.Date), x.LoadNumber, x.PackageLabel, Number(x.Pieces), Number(x.InputWidth), Number(x.InputThickness), Number(x.InputLength), x.Quality, x.FinishedRawValue, x.ExclusionReason, x.Severity.ToString() }));
        Write(paths[1], ["Riga Excel", "Tipo", "Problema", "Gravità", "Azione proposta"], report.Issues.Select(x => new[] { x.ExcelRow.ToString(), x.Type, x.Problem, x.Severity.ToString(), x.ProposedAction }));
        Write(paths[2], ["Nome originale", "Numero righe", "Prima data", "Ultima data", "Nome simile"], report.SupplierReviews.Select(x => new[] { x.OriginalName, x.Rows.ToString(), Date(x.FirstDate), Date(x.LastDate), x.SimilarName }));
        Write(paths[3], ["Valore originale", "Numero righe", "MC fisici complessivi", "Prima data", "Ultima data"], report.LegacyQualityReviews.Select(x => new[] { x.OriginalValue, x.Rows.ToString(), Number(x.PhysicalCubicMeters), Date(x.FirstDate), Date(x.LastDate) }));
        return paths;
    }
    private static void Write(string path, string[] headers, IEnumerable<string?[]> rows)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine(string.Join(';', headers.Select(Escape)));
        foreach (var row in rows) writer.WriteLine(string.Join(';', row.Select(Escape)));
    }
    private static string Escape(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    private static string Date(DateTime? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "";
    private static string Number(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";
}
