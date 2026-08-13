using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class LoadNumberSequenceService
{
    private static readonly Lazy<LoadNumberSequenceService> SharedInstance = new(() => new());
    private readonly object _assignmentLock = new();
    private readonly Dictionary<(Guid SupplierId, int Year), int> _lastAssigned = [];
    private LoadNumberSequenceService() { }
    public static LoadNumberSequenceService Shared => SharedInstance.Value;

    public LoadNumberAssignment PreviewNext(Guid supplierId, int year)
    {
        lock (_assignmentLock)
            return new(supplierId, year, _lastAssigned.GetValueOrDefault((supplierId, year)) + 1);
    }

    // In memoria il lock rende atomica l'assegnazione. Nel repository SQL futuro questa
    // operazione dovrà essere una transazione con vincolo UNIQUE (SupplierId, Year, AnnualSequence).
    public LoadNumberAssignment ReserveNext(Guid supplierId, int year)
    {
        lock (_assignmentLock)
        {
            var key = (supplierId, year);
            var next = _lastAssigned.GetValueOrDefault(key) + 1;
            _lastAssigned[key] = next;
            return new(supplierId, year, next);
        }
    }
}
