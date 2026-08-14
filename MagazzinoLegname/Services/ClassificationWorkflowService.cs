using System.Collections.ObjectModel;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class ClassificationWorkflowService
{
    public static ClassificationWorkflowService Shared { get; } = new();
    private ClassificationWorkflowService()
    {
        Loads = new(new ClassificationDemoService().CreateLoads());
        var demoGroup = Loads.SelectMany(load => load.Groups).First(group => group.IsClassified);
        const int discardedBoards = 18;
        const decimal partialPercentage = 3.5m;
        var result = new WasteAdjustmentCalculationService().Calculate(
            demoGroup, discardedBoards, partialPercentage);
        AddAdjustment(demoGroup, new WasteAdjustment
        {
            LoadId = demoGroup.LoadId, MaterialGroupId = demoGroup.GroupId,
            AdjustmentDate = DateTime.Today.AddHours(10), AdjustmentOperator = "Elena Bianchi",
            InitialPieces = result.InitialGroupPieces, DiscardedWholeBoards = result.DiscardedWholeBoards,
            GoodPieces = result.GoodGroupPieces, TheoreticalUsefulCubicMeters = result.TheoreticalUsefulCubicMeters,
            CubicMetersAfterWholeBoardWaste = result.CubicMetersAfterWholeBoardWaste,
            PartialWastePercentage = result.PartialWastePercentage, PartialWasteCubicMeters = result.PartialWasteCubicMeters,
            RealAvailableCubicMeters = result.RealAvailableCubicMeters,
            WholeBoardWastePercentage = result.WholeBoardWastePercentage,
            TotalClassificationWastePercentage = result.TotalQualityWastePercentage
        });
    }

    public ObservableCollection<ClassificationLoad> Loads { get; }
    public ObservableCollection<PhysicalPackageDraft> RegisteredPhysicalPackages { get; } = [];
    public ObservableCollection<ClassificationMovement> ClassificationHistory { get; } = [];
    public ObservableCollection<WasteAdjustment> WasteAdjustmentHistory { get; } = [];
    public event EventHandler? WorkflowChanged;

    public void AddAdjustment(MaterialGroupClassification group, WasteAdjustment adjustment)
    {
        if (group.WasteVerified) return;
        WasteAdjustmentHistory.Add(adjustment);
        group.MarkWasteAsVerified();
        WorkflowChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyClassificationChanged() => WorkflowChanged?.Invoke(this, EventArgs.Empty);

    public void RecordClassification(MaterialGroupClassification group)
    {
        if (!group.ClassificationDate.HasValue || string.IsNullOrWhiteSpace(group.ClassificationOperator)) return;
        ClassificationHistory.Add(new ClassificationMovement
        {
            LoadId = group.LoadId,
            MaterialGroupId = group.GroupId,
            ClassificationDate = group.ClassificationDate.Value,
            ClassificationOperator = group.ClassificationOperator
        });
        WorkflowChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RegisterLoad(ClassificationLoad load, IReadOnlyList<PhysicalPackageDraft> packages)
    {
        if (Loads.Any(item => item.SupplierCode.Equals(load.SupplierCode, StringComparison.OrdinalIgnoreCase)
            && item.LoadNumber.Equals(load.LoadNumber, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Il numero carico è già presente per questo fornitore.");
        var existingCodes = RegisteredPhysicalPackages.Select(item => item.PackageCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (packages.Any(package => !existingCodes.Add(package.PackageCode)))
            throw new InvalidOperationException("È stato rilevato un CodicePacco duplicato.");
        Loads.Add(load);
        foreach (var package in packages) RegisteredPhysicalPackages.Add(package);
        WorkflowChanged?.Invoke(this, EventArgs.Empty);
    }
}
