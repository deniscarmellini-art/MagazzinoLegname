using System.Collections.ObjectModel;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class HistoryViewModel : ObservableObject
{
    private readonly ClassificationWorkflowService _workflow = ClassificationWorkflowService.Shared;
    private readonly InventoryProjectionService _inventory = InventoryProjectionService.Shared;
    private readonly LegacyHistoricalStore _legacyHistory = LegacyHistoricalStore.Shared;
    private IReadOnlyList<HistoryMovementRow> _allMovements = [];
    private DateTime? _fromDate = DateTime.Today.AddYears(-1);
    private DateTime? _toDate = DateTime.Today;
    private string _selectedSupplier = "Tutti";
    private string _selectedMovementType = "Tutti";
    private string _selectedThickness = "Tutti";
    private string _selectedQuality = "Tutte";
    private string _selectedOperator = "Tutti";
    private string _searchText = string.Empty;
    private string _quickFilter = "Tutti";
    private HistoryMovementRow? _selectedMovement;
    private LoadHistorySummary? _selectedLoadSummary;

    public HistoryViewModel()
    {
        _workflow.WorkflowChanged += (_, _) => Reload();
        _inventory.InventoryChanged += (_, _) => Reload();
        _legacyHistory.HistoryChanged += (_, _) => Reload();
        Reload();
    }

    public ObservableCollection<HistoryMovementRow> VisibleMovements { get; } = [];
    public ObservableCollection<string> Suppliers { get; } = [];
    public ObservableCollection<string> Thicknesses { get; } = [];
    public ObservableCollection<string> Operators { get; } = [];
    public IReadOnlyList<string> MovementTypes { get; } = ["Tutti", "Entrata", "Classificazione", "Rettifica scarti", "Scarico", "Reso", "Rimozione manuale"];
    public IReadOnlyList<string> Qualities { get; } = ["Tutte", "C", "VISTA"];
    public IReadOnlyList<string> QuickFilters { get; } = ["Tutti", "Entrate", "Rettifiche", "Scarichi", "Resi", "Rimozioni manuali"];
    public ObservableCollection<HistoryMovementRow> LoadTimeline { get; } = [];
    public ObservableCollection<LoadPackageHistoryRow> LoadPackages { get; } = [];

    public DateTime? FromDate { get => _fromDate; set { if (SetProperty(ref _fromDate, value)) ApplyFilters(); } }
    public DateTime? ToDate { get => _toDate; set { if (SetProperty(ref _toDate, value)) ApplyFilters(); } }
    public string SelectedSupplier { get => _selectedSupplier; set { if (SetProperty(ref _selectedSupplier, value)) ApplyFilters(); } }
    public string SelectedMovementType { get => _selectedMovementType; set { if (SetProperty(ref _selectedMovementType, value)) ApplyFilters(); } }
    public string SelectedThickness { get => _selectedThickness; set { if (SetProperty(ref _selectedThickness, value)) ApplyFilters(); } }
    public string SelectedQuality { get => _selectedQuality; set { if (SetProperty(ref _selectedQuality, value)) ApplyFilters(); } }
    public string SelectedOperator { get => _selectedOperator; set { if (SetProperty(ref _selectedOperator, value)) ApplyFilters(); } }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) ApplyFilters(); } }
    public string QuickFilter { get => _quickFilter; set { if (SetProperty(ref _quickFilter, value)) ApplyFilters(); } }
    public HistoryMovementRow? SelectedMovement { get => _selectedMovement; set => SetProperty(ref _selectedMovement, value); }
    public LoadHistorySummary? SelectedLoadSummary { get => _selectedLoadSummary; private set { SetProperty(ref _selectedLoadSummary, value); OnPropertyChanged(nameof(IsLoadHistoryVisible)); } }
    public bool IsLoadHistoryVisible => SelectedLoadSummary is not null;

    public void SelectLoad(Guid loadId)
    {
        var load = _workflow.Loads.FirstOrDefault(item => item.Id == loadId);
        if (load is null)
        {
            var legacyRecords = _legacyHistory.Records.Where(item => item.HistoricalLoadId == loadId).ToArray();
            if (legacyRecords.Length == 0) return;
            LoadTimeline.Clear();
            foreach (var movement in _allMovements.Where(item => item.LoadId == loadId).OrderBy(item => item.DateTime)) LoadTimeline.Add(movement);
            LoadPackages.Clear();
            SelectedLoadSummary = new LoadHistorySummary { LoadNumber = legacyRecords[0].LoadNumber,
                SupplierName = legacyRecords[0].SupplierName, InitialPackages = legacyRecords.Length,
                PresentPackages = 0, DischargedPackages = legacyRecords.Length,
                IncomingCubicMeters = legacyRecords.Sum(item => item.PhysicalCubicMeters),
                DischargedCubicMeters = legacyRecords.Where(item => item.FinishedOn.HasValue).Sum(item => item.LegacyAvailableCubicMeters),
                PresentCubicMeters = 0m };
            return;
        }
        var packages = _inventory.BuildInventory(true).Where(item => item.LoadId == loadId).OrderBy(item => item.PackageNumber).ToList();
        LoadTimeline.Clear();
        foreach (var movement in _allMovements.Where(item => item.LoadId == loadId).OrderBy(item => item.DateTime)) LoadTimeline.Add(movement);
        LoadPackages.Clear();
        foreach (var package in packages)
            LoadPackages.Add(new LoadPackageHistoryRow(package));

        var adjustments = _workflow.WasteAdjustmentHistory.Where(item => item.LoadId == loadId).ToList();
        SelectedLoadSummary = new LoadHistorySummary
        {
            LoadNumber = load.LoadNumber,
            SupplierName = load.SupplierName,
            InitialPackages = load.TotalPackages,
            PresentPackages = packages.Count(item => item.PackageStatus == "Presente"),
            DischargedPackages = packages.Count(item => item.PackageStatus == "Scaricato"),
            ManuallyRemovedPackages = packages.Count(item => item.PackageStatus == "Rimosso manualmente"),
            IncomingCubicMeters = load.Groups.Sum(item => item.IncomingPhysicalCubicMeters),
            RealCubicMeters = adjustments.Sum(item => item.RealAvailableCubicMeters),
            DischargedCubicMeters = packages.Sum(item => item.DischargedCubicMeters ?? 0m),
            ManuallyRemovedCubicMeters = packages.Sum(item => item.ManuallyRemovedCubicMeters ?? 0m),
            PresentCubicMeters = packages.Where(item => item.IsPresent).Sum(item => item.InventoryCubicMeters)
        };
    }

    public void CloseLoadHistory() => SelectedLoadSummary = null;

    private void Reload()
    {
        _allMovements = BuildMovementRows().OrderByDescending(item => item.DateTime).ToList();
        ReplaceOptions(Suppliers, "Tutti", _allMovements.Select(item => item.SupplierName));
        ReplaceOptions(Thicknesses, "Tutti", _allMovements.Where(item => item.ConventionalThickness.HasValue)
            .Select(item => item.ConventionalThickness!.Value.ToString("0")));
        ReplaceOptions(Operators, "Tutti", _allMovements.Select(item => item.Operator));
        EnsureValidSelection(ref _selectedSupplier, Suppliers, "Tutti", nameof(SelectedSupplier));
        EnsureValidSelection(ref _selectedThickness, Thicknesses, "Tutti", nameof(SelectedThickness));
        EnsureValidSelection(ref _selectedOperator, Operators, "Tutti", nameof(SelectedOperator));
        ApplyFilters();
        if (SelectedLoadSummary is not null)
        {
            var load = _workflow.Loads.FirstOrDefault(item => item.LoadNumber == SelectedLoadSummary.LoadNumber
                && item.SupplierName == SelectedLoadSummary.SupplierName);
            if (load is not null) SelectLoad(load.Id);
        }
    }

    internal static IEnumerable<HistoryMovementRow> BuildMovementRows()
    {
        var workflow = ClassificationWorkflowService.Shared;
        var inventory = InventoryProjectionService.Shared;

        foreach (var aggregate in LegacyHistoricalStore.Shared.Records.GroupBy(item => new
        {
            item.HistoricalLoadId, item.ArrivalDate, item.SupplierName, item.LoadNumber,
            item.IncomingThickness, item.IncomingWidth, item.IncomingLength,
            Quality = item.QualityOriginal ?? item.QualityNormalized ?? "—"
        }))
        {
            var rows = aggregate.OrderBy(item => item.ExcelRow).ToArray();
            var conventional = MaterialParametersService.Shared.Parameters.FindFamily(aggregate.Key.IncomingThickness)?.ConventionalThickness;
            var detailRows = string.Join("\n", rows.Select(item =>
                $"Riga Excel {item.ExcelRow} · Pacco {item.PackageLabel ?? "—"} · Pezzi {item.Pieces:N0} · MC {item.PhysicalCubicMeters:N5} · Finito il {item.FinishedRawValue}"));
            var detail = $"Origine: Importazione legacy\nCarico storico: {aggregate.Key.LoadNumber}\nFornitore: {aggregate.Key.SupplierName}\n" +
                $"Data arrivo: {aggregate.Key.ArrivalDate:dd/MM/yyyy}\nMateriale: {aggregate.Key.IncomingThickness:N2} × {aggregate.Key.IncomingWidth:N2} × {aggregate.Key.IncomingLength:N2}\n" +
                $"Qualità originale: {aggregate.Key.Quality}\nPacchi: {rows.Length:N0}\nPezzi: {rows.Sum(item => item.Pieces):N0}\nMC fisici: {rows.Sum(item => item.PhysicalCubicMeters):N5}\n\n{detailRows}";
            yield return new HistoryMovementRow(aggregate.Key.ArrivalDate, "Entrata", aggregate.Key.HistoricalLoadId,
                null, aggregate.Key.LoadNumber, aggregate.Key.SupplierName,
                $"{aggregate.Key.IncomingThickness:0} × {aggregate.Key.IncomingWidth:0} × {aggregate.Key.IncomingLength:0}",
                conventional, aggregate.Key.Quality, $"{rows.Length} pacchi", rows.Sum(item => item.PhysicalCubicMeters),
                "Importazione legacy", "—", detail);
        }

        foreach (var aggregate in LegacyHistoricalStore.Shared.Records.Where(item => item.FinishedOn.HasValue && !item.IsSupplierReturn).GroupBy(item => new
        {
            item.HistoricalLoadId, FinishedOn = item.FinishedOn!.Value.Date,
            item.SupplierName, item.LoadNumber, item.IncomingThickness, item.IncomingWidth, item.IncomingLength,
            Quality = item.QualityOriginal ?? item.QualityNormalized ?? "—"
        }))
        {
            var rows = aggregate.OrderBy(item => item.ExcelRow).ToArray();
            var conventional = MaterialParametersService.Shared.Parameters.FindFamily(aggregate.Key.IncomingThickness)?.ConventionalThickness;
            var cubicMeters = rows.Sum(item => item.LegacyAvailableCubicMeters);
            var arrivalDates = string.Join(", ", rows.Select(item => item.ArrivalDate.ToString("dd/MM/yyyy")).Distinct().Order());
            var detailRows = string.Join("\n", rows.Select(item =>
                $"Riga Excel {item.ExcelRow} · Pacco {item.PackageLabel ?? "—"} · Pezzi {item.Pieces:N0} · MC scaricati {item.LegacyAvailableCubicMeters:N5}"));
            var detail = $"Origine: Importazione legacy\nTipo movimento: Scarico\nCarico storico: {aggregate.Key.LoadNumber}\n" +
                $"Fornitore: {aggregate.Key.SupplierName}\nData/Ora: {aggregate.Key.FinishedOn:dd/MM/yyyy}\nData arrivo originale: {arrivalDates}\n" +
                $"Materiale: {aggregate.Key.IncomingThickness:N2} × {aggregate.Key.IncomingWidth:N2} × {aggregate.Key.IncomingLength:N2}\n" +
                $"Qualità originale: {aggregate.Key.Quality}\nPacchi: {rows.Length:N0}\nPezzi: {rows.Sum(item => item.Pieces):N0}\nMC scaricati: {cubicMeters:N5}\n\n{detailRows}";
            yield return new HistoryMovementRow(aggregate.Key.FinishedOn, "Scarico", aggregate.Key.HistoricalLoadId,
                null, aggregate.Key.LoadNumber, aggregate.Key.SupplierName,
                $"{aggregate.Key.IncomingThickness:0} × {aggregate.Key.IncomingWidth:0} × {aggregate.Key.IncomingLength:0}",
                conventional, aggregate.Key.Quality, $"{rows.Length} pacchi", -cubicMeters,
                "Importazione legacy", "—", detail);
        }

        foreach (var aggregate in LegacyHistoricalStore.Shared.Records.Where(item => item.IsSupplierReturn).GroupBy(item => new
        {
            item.HistoricalLoadId, ReturnDate = item.FinishedOn?.Date,
            item.SupplierName, item.LoadNumber, item.IncomingThickness, item.IncomingWidth, item.IncomingLength,
            Quality = item.QualityOriginal ?? item.QualityNormalized ?? "—"
        }))
        {
            var rows = aggregate.OrderBy(item => item.ExcelRow).ToArray();
            var conventional = MaterialParametersService.Shared.Parameters.FindFamily(aggregate.Key.IncomingThickness)?.ConventionalThickness;
            var physical = rows.Sum(item => item.PhysicalCubicMeters);
            var loadRecords = LegacyHistoricalStore.Shared.Records.Where(item => item.HistoricalLoadId == aggregate.Key.HistoricalLoadId).ToArray();
            var mode = loadRecords.All(item => item.IsSupplierReturn) ? "Reso totale" : "Reso parziale";
            var closureValues = string.Join(", ", rows.Select(item => item.FinishedRawValue).Distinct(StringComparer.OrdinalIgnoreCase));
            yield return new HistoryMovementRow(aggregate.Key.ReturnDate, "Reso", aggregate.Key.HistoricalLoadId,
                null, aggregate.Key.LoadNumber, aggregate.Key.SupplierName,
                $"{aggregate.Key.IncomingThickness:0} × {aggregate.Key.IncomingWidth:0} × {aggregate.Key.IncomingLength:0}",
                conventional, aggregate.Key.Quality, $"{rows.Length} pacchi", -physical,
                "Importazione legacy", "—",
                $"Origine: Importazione legacy\nTipo movimento: Reso a fornitore\nModalità: {mode}\nData/Ora: {(aggregate.Key.ReturnDate.HasValue ? aggregate.Key.ReturnDate.Value.ToString("dd/MM/yyyy") : "N/D")}\n" +
                $"Carico: {aggregate.Key.LoadNumber}\nFornitore: {aggregate.Key.SupplierName}\nMateriale: {aggregate.Key.IncomingThickness:N2} × {aggregate.Key.IncomingWidth:N2} × {aggregate.Key.IncomingLength:N2}\n" +
                $"Qualità: {aggregate.Key.Quality}\nPacchi: {rows.Length:N0}\nPezzi: {rows.Sum(item => item.Pieces):N0}\nMC fisici resi: {physical:N5}\nChiusura legacy: {closureValues}");
        }

        foreach (var load in workflow.Loads)
        {
            var material = string.Join(" · ", load.Groups.Select(group => $"{group.ConventionalThickness:0} {group.Quality}").Distinct());
            yield return new HistoryMovementRow(load.ArrivalDate, "Entrata", load.Id, null, load.LoadNumber,
                load.SupplierName, material, null, null, $"{load.TotalPackages} pacchi",
                load.Groups.Sum(group => group.IncomingPhysicalCubicMeters), load.ReceiptOperator,
                load.DeliveryNoteNumber, BuildEntryDetail(load),
                load.Groups.Select(group => group.ConventionalThickness).Distinct().ToArray(),
                load.Groups.Select(group => group.Quality).Distinct().ToArray());

            foreach (var group in load.Groups)
            {
                var recorded = workflow.ClassificationHistory.Where(item => item.MaterialGroupId == group.GroupId).ToList();
                if (recorded.Count == 0 && group.ClassificationDate.HasValue)
                    recorded.Add(new ClassificationMovement { LoadId = load.Id, MaterialGroupId = group.GroupId,
                        ClassificationDate = group.ClassificationDate.Value,
                        ClassificationOperator = group.ClassificationOperator ?? "—" });
                foreach (var movement in recorded)
                    yield return new HistoryMovementRow(movement.ClassificationDate, "Classificazione", load.Id,
                        group.GroupId, load.LoadNumber, load.SupplierName, Material(group), group.ConventionalThickness,
                        group.Quality, $"{group.PackageCount} pacchi", 0m, movement.ClassificationOperator,
                        load.DeliveryNoteNumber, $"Gruppo: {Material(group)}\nPacchi: {group.PackageCount}\nPezzi: {group.InitialPieces}\nData/ora: {movement.ClassificationDate:dd/MM/yyyy HH:mm}\nOperatore: {movement.ClassificationOperator}");
            }
        }

        foreach (var adjustment in workflow.WasteAdjustmentHistory)
        {
            var (load, group) = FindGroup(workflow, adjustment.LoadId, adjustment.MaterialGroupId);
            if (load is null || group is null) continue;
            yield return new HistoryMovementRow(adjustment.AdjustmentDate, "Rettifica scarti", load.Id,
                group.GroupId, load.LoadNumber, load.SupplierName, Material(group), group.ConventionalThickness,
                group.Quality, $"{group.PackageCount} pacchi",
                adjustment.RealAvailableCubicMeters - adjustment.AdjustmentBaseCubicMeters,
                adjustment.AdjustmentOperator, load.DeliveryNoteNumber, BuildAdjustmentDetail(load, group, adjustment));
        }

        foreach (var discharge in inventory.DischargeMovements)
        {
            var (load, group) = FindGroup(workflow, discharge.LoadId, discharge.MaterialGroupId);
            if (load is null || group is null) continue;
            var package = inventory.FindPackage(discharge.PackageCode);
            yield return new HistoryMovementRow(discharge.DischargeDate, "Scarico", load.Id, group.GroupId,
                load.LoadNumber, load.SupplierName, Material(group), group.ConventionalThickness, group.Quality,
                package?.PackagePosition ?? discharge.PackageCode, -discharge.DischargedCubicMeters,
                discharge.DischargeOperator, load.DeliveryNoteNumber,
                $"Codice pacco: {discharge.PackageCode}\nCarico: {load.LoadNumber}\nFornitore: {load.SupplierName}\nPacco: {package?.PackagePosition ?? "—"}\nMateriale: {Material(group)}\nQualità: {group.Quality}\nMC scaricati: {discharge.DischargedCubicMeters:N2}\nData/ora: {discharge.DischargeDate:dd/MM/yyyy HH:mm}\nOperatore: {discharge.DischargeOperator}");
        }

        foreach (var removal in inventory.ManualRemovalMovements)
        {
            var (load, group) = FindGroup(workflow, removal.LoadId, removal.MaterialGroupId);
            if (load is null || group is null) continue;
            yield return new HistoryMovementRow(removal.RemovalDate, "Rimozione manuale", load.Id, group.GroupId,
                load.LoadNumber, load.SupplierName, Material(group), group.ConventionalThickness, group.Quality,
                removal.PackageCode, -removal.RemovedCubicMeters, removal.RemovalOperator, load.DeliveryNoteNumber,
                $"Codice pacco: {removal.PackageCode}\nCarico: {load.LoadNumber}\nFornitore: {load.SupplierName}\nMateriale: {Material(group)}\nQualità: {group.Quality}\nMC rimossi: {removal.RemovedCubicMeters:N2}\nData/ora: {removal.RemovalDate:dd/MM/yyyy HH:mm}\nOperatore: {removal.RemovalOperator}\nMotivo: {removal.Reason}\nNota: {(string.IsNullOrWhiteSpace(removal.Note) ? "—" : removal.Note)}");
        }

        foreach (var returnGroup in inventory.SupplierReturnMovements
            .GroupBy(item => new { item.ReturnOperationId, item.MaterialGroupId }))
        {
            var movements = returnGroup.OrderBy(item => item.PackageCode).ToArray();
            var first = movements[0];
            var (load, group) = FindGroup(workflow, first.LoadId, first.MaterialGroupId);
            if (load is null || group is null) continue;
            var physical = movements.Sum(item => item.ReturnedPhysicalCubicMeters);
            var removed = movements.Sum(item => item.RemovedInventoryCubicMeters);
            yield return new HistoryMovementRow(first.ReturnDate, "Reso", load.Id, group.GroupId,
                load.LoadNumber, load.SupplierName, Material(group), group.ConventionalThickness, group.Quality,
                movements.Length == 1 ? first.PackageCode : $"{movements.Length} pacchi",
                -physical, first.ReturnOperator, first.DocumentReference,
                $"Tipo movimento: Reso a fornitore\nModalità: {(first.Mode == SupplierReturnMode.Total ? "Reso totale" : "Reso parziale")}\n" +
                $"Carico: {load.LoadNumber}\nFornitore: {load.SupplierName}\nPacchi: {string.Join(", ", movements.Select(item => item.PackageCode))}\n" +
                $"Materiale: {Material(group)}\nQualità: {group.Quality}\nMC fisici resi: {physical:N5}\nMC rimossi dalla giacenza: {removed:N5}\n" +
                $"Data/ora: {first.ReturnDate:dd/MM/yyyy HH:mm}\nOperatore: {first.ReturnOperator}\nMotivo: {first.Reason}\n" +
                $"Note: {(string.IsNullOrWhiteSpace(first.Note) ? "—" : first.Note)}\nDocumento reso: {(string.IsNullOrWhiteSpace(first.DocumentReference) ? "—" : first.DocumentReference)}");
        }
    }

    private void ApplyFilters()
    {
        var quickType = QuickFilter switch { "Entrate" => "Entrata", "Rettifiche" => "Rettifica scarti",
            "Scarichi" => "Scarico", "Resi" => "Reso", "Rimozioni manuali" => "Rimozione manuale", _ => null };
        var query = _allMovements
            .Where(item => !item.DateTime.HasValue || !FromDate.HasValue || item.DateTime.Value.Date >= FromDate.Value.Date)
            .Where(item => !item.DateTime.HasValue || !ToDate.HasValue || item.DateTime.Value.Date <= ToDate.Value.Date)
            .Where(item => SelectedSupplier == "Tutti" || item.SupplierName == SelectedSupplier)
            .Where(item => SelectedMovementType == "Tutti" || item.MovementType == SelectedMovementType)
            .Where(item => quickType is null || item.MovementType == quickType)
            .Where(item => SelectedThickness == "Tutti" || item.ConventionalThickness?.ToString("0") == SelectedThickness
                || item.RelatedThicknesses?.Any(value => value.ToString("0") == SelectedThickness) == true)
            .Where(item => SelectedQuality == "Tutte" || item.Quality == SelectedQuality
                || item.RelatedQualities?.Contains(SelectedQuality) == true)
            .Where(item => SelectedOperator == "Tutti" || item.Operator == SelectedOperator)
            .Where(item => string.IsNullOrWhiteSpace(SearchText)
                || item.LoadNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || item.DeliveryNoteNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || item.PackageDisplay.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || item.DetailText.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        VisibleMovements.Clear();
        foreach (var item in query) VisibleMovements.Add(item);
        SelectedMovement = VisibleMovements.FirstOrDefault();
    }

    private static (ClassificationLoad? Load, MaterialGroupClassification? Group) FindGroup(
        ClassificationWorkflowService workflow, Guid loadId, Guid groupId)
    {
        var load = workflow.Loads.FirstOrDefault(item => item.Id == loadId);
        return (load, load?.Groups.FirstOrDefault(item => item.GroupId == groupId));
    }

    private static string Material(MaterialGroupClassification group) =>
        $"{group.ConventionalThickness:0} × {group.WidthAfterPlaning:0} × {group.IncomingLength:0}";

    private static string BuildEntryDetail(ClassificationLoad load) =>
        $"Numero carico: {load.LoadNumber}\nNumero DDT: {(string.IsNullOrWhiteSpace(load.DeliveryNoteNumber) ? "—" : load.DeliveryNoteNumber)}\nFornitore: {load.SupplierName}\nData arrivo: {load.ArrivalDate:dd/MM/yyyy}\nOperatore: {(string.IsNullOrWhiteSpace(load.ReceiptOperator) ? "—" : load.ReceiptOperator)}\nCertificazione: {load.Certification}\n\n" +
        string.Join("\n\n", load.Groups.Select((group, index) =>
            $"GRUPPO {index + 1} · Qualità {group.Quality}\nSpessore ingresso / convenzionale / utile: {group.IncomingThickness:N2} / {group.ConventionalThickness:N2} / {group.UsefulThickness:N2}\nLarghezza ingresso / dopo prepiallatura: {group.IncomingWidth:N2} / {group.WidthAfterPlaning:N2}\nLunghezza: {group.IncomingLength:N2}\nPacchi: {group.PackageCount} · Pezzi: {group.InitialPieces}\nMC fisici: {group.IncomingPhysicalCubicMeters:N2}\nPrezzo applicato: {group.AppliedPrice:N2} €/m³ · Valore: {group.LineValue:N2} €"));

    private static string BuildAdjustmentDetail(ClassificationLoad load, MaterialGroupClassification group, WasteAdjustment item) =>
        $"Carico: {load.LoadNumber}\nGruppo: {Material(group)} · Qualità {group.Quality}\nPezzi iniziali: {item.InitialPieces}\nTavole intere scartate: {item.DiscardedWholeBoards}\nPezzi buoni: {item.GoodPieces}\nScarto tavole: {item.WholeBoardWastePercentage:N2}%\nScarto parziale: {item.PartialWastePercentage:N2}%\nScarto qualità complessivo: {item.TotalClassificationWastePercentage:N2}%\nMC fisici base rettifica: {item.AdjustmentBaseCubicMeters:N2}\nMC dopo scarto tavole: {item.CubicMetersAfterWholeBoardWaste:N2}\nMC scarto parziale: {item.PartialWasteCubicMeters:N2}\nMC reali disponibili: {item.RealAvailableCubicMeters:N2}\nData/ora: {item.AdjustmentDate:dd/MM/yyyy HH:mm}\nOperatore: {item.AdjustmentOperator}";

    private static void ReplaceOptions(ObservableCollection<string> target, string first, IEnumerable<string> values)
    {
        target.Clear(); target.Add(first);
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value) && value != "—").Distinct().Order()) target.Add(value);
    }

    private void EnsureValidSelection(ref string field, IEnumerable<string> options, string fallback, string propertyName)
    {
        if (!options.Contains(field)) field = fallback;
        OnPropertyChanged(propertyName);
    }
}

public sealed record HistoryMovementRow(DateTime? DateTime, string MovementType, Guid LoadId,
    Guid? MaterialGroupId, string LoadNumber, string SupplierName, string Material,
    decimal? ConventionalThickness, string? Quality, string PackageDisplay, decimal CubicMeters,
    string Operator, string DeliveryNoteNumber, string DetailText,
    IReadOnlyCollection<decimal>? RelatedThicknesses = null,
    IReadOnlyCollection<string>? RelatedQualities = null)
{
    public string CubicMetersDisplay => CubicMeters == 0m ? "—" : $"{CubicMeters:+0.00;-0.00;0.00}";
}

public sealed class LoadHistorySummary
{
    public required string LoadNumber { get; init; }
    public required string SupplierName { get; init; }
    public int InitialPackages { get; init; }
    public int PresentPackages { get; init; }
    public int DischargedPackages { get; init; }
    public int ManuallyRemovedPackages { get; init; }
    public decimal IncomingCubicMeters { get; init; }
    public decimal RealCubicMeters { get; init; }
    public decimal DischargedCubicMeters { get; init; }
    public decimal ManuallyRemovedCubicMeters { get; init; }
    public decimal PresentCubicMeters { get; init; }
}

public sealed class LoadPackageHistoryRow
{
    public LoadPackageHistoryRow(InventoryPackage package)
    {
        PackageCode = package.PackageCode; Position = package.PackagePosition; Quality = package.Quality;
        Measure = package.OperationalMeasure; Status = package.PackageStatus;
        EventDate = package.DischargeDate ?? package.ManualRemovalDate;
        EventCubicMeters = package.DischargedCubicMeters ?? package.ManuallyRemovedCubicMeters;
        Reason = package.ManualRemovalReason;
    }
    public string PackageCode { get; }
    public string Position { get; }
    public string Quality { get; }
    public string Measure { get; }
    public string Status { get; }
    public DateTime? EventDate { get; }
    public decimal? EventCubicMeters { get; }
    public string? Reason { get; }
}
