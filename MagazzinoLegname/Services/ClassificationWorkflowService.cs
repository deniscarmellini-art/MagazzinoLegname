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
        var goodPieces = demoGroup.InitialPieces - discardedBoards;
        var afterWholeBoards = demoGroup.Volume(goodPieces);
        var partialWaste = afterWholeBoards * partialPercentage / 100m;
        AddAdjustment(demoGroup, new WasteAdjustment
        {
            LoadId = demoGroup.LoadId, MaterialGroupId = demoGroup.GroupId,
            AdjustmentDate = DateTime.Today.AddHours(10), AdjustmentOperator = "Elena Bianchi",
            InitialPieces = demoGroup.InitialPieces, DiscardedWholeBoards = discardedBoards,
            GoodPieces = goodPieces, TheoreticalUsefulCubicMeters = demoGroup.TheoreticalUsefulCubicMeters,
            CubicMetersAfterWholeBoardWaste = afterWholeBoards,
            PartialWastePercentage = partialPercentage, PartialWasteCubicMeters = partialWaste,
            RealAvailableCubicMeters = afterWholeBoards - partialWaste,
            WholeBoardWastePercentage = (decimal)discardedBoards / demoGroup.InitialPieces * 100m,
            TotalClassificationWastePercentage = (demoGroup.TheoreticalUsefulCubicMeters - (afterWholeBoards - partialWaste)) / demoGroup.TheoreticalUsefulCubicMeters * 100m
        });
    }

    public ObservableCollection<ClassificationLoad> Loads { get; }
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
}
