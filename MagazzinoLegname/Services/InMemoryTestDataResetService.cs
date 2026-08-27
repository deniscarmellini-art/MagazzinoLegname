namespace MagazzinoLegname.Services;

/// <summary>Utility esclusiva per l'attuale store temporaneo di test; non destinata alla persistenza produttiva.</summary>
public sealed class InMemoryTestDataResetService
{
    public static InMemoryTestDataResetService Shared { get; } = new();
    private readonly object _sync = new();
    private InMemoryTestDataResetService() { }

    public void ResetOperationalData()
    {
        lock (_sync)
        {
            InventoryProjectionService.Shared.ResetOperationalTestData();
            ClassificationWorkflowService.Shared.ResetOperationalTestData();
            LegacyInitialInventoryImportService.Shared.ResetTestImportRegistry();
            LegacyHistoricalStore.Shared.ResetTestImportRegistry();
        }
    }
}
