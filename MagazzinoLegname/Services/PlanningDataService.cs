using System.Collections.ObjectModel;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Repositories;

namespace MagazzinoLegname.Services;

public sealed class PlanningDataService
{
    public static PlanningDataService Shared { get; } = new(new SqlPlannedArrivalRepository(SqlPersistenceRoot.ContextFactory));
    private readonly IPlannedArrivalRepository _repository;
    private readonly ObservableCollection<PlannedArrival> _arrivals = [];
    public PlanningDataService(IPlannedArrivalRepository repository) { _repository = repository; Arrivals = new(_arrivals); }

    public ReadOnlyObservableCollection<PlannedArrival> Arrivals { get; }
    public ObservableCollection<PlannedConsumption> Consumptions { get; } = [];
    public event EventHandler? PlanningChanged;

    public bool IsAvailable { get; private set; }
    public void Invalidate() { IsAvailable = false; _arrivals.Clear(); PlanningChanged?.Invoke(this, EventArgs.Empty); }
    public void Reload()
    {
        IsAvailable = false; _arrivals.Clear();
        try { foreach (var arrival in _repository.GetAll()) _arrivals.Add(arrival); IsAvailable = true; }
        finally { PlanningChanged?.Invoke(this, EventArgs.Empty); }
    }
    public PlannedArrival GetOrCreateArrival(Guid supplierId, DateTime date) =>
        _arrivals.FirstOrDefault(x => x.SupplierId == supplierId && x.Date.Date == date.Date)?.Copy()
        ?? new PlannedArrival { SupplierId = supplierId, Date = date.Date };
    public void Save(PlannedArrival arrival) => Execute(() => _repository.Save(arrival));
    public void Delete(PlannedArrival arrival) => Execute(() => _repository.Delete(arrival));
    public void ConfirmArrival(Guid arrivalId, string? operatorName = null)
    {
        var arrival = _arrivals.FirstOrDefault(x => x.Id == arrivalId)?.Copy();
        if (arrival is null) throw new InvalidOperationException("Ricaricare le pianificazioni da SQL.");
        if (arrival.Status == PlannedArrivalStatus.Confirmed) return;
        arrival.Confirm(DateTime.Now, operatorName); Save(arrival);
    }
    private void Execute(Action operation)
    {
        if (!IsAvailable) throw new InvalidOperationException("Ricaricare le pianificazioni da SQL prima di modificare.");
        try { operation(); }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine($"[Pianificazione] {error}");
            var message = error is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException
                ? "Conflitto: pianificazione modificata da un'altra postazione. Nessuna sovrascrittura." : error.Message;
            try { Reload(); } catch (Exception reload) { throw new InvalidOperationException(message + " Ricaricamento SQL fallito: " + reload.Message, error); }
            throw new InvalidOperationException(message + " Dati ricaricati da SQL: verificare prima di riprovare.", error);
        }
        try { Reload(); } catch (Exception error) { throw new InvalidOperationException("Operazione salvata in SQL, ma ricaricamento fallito. Aggiornare prima di altre modifiche.", error); }
    }

    public PlannedConsumption GetOrCreateConsumption(DateTime weekStart, decimal thickness, string quality)
    {
        var consumption = Consumptions.FirstOrDefault(item => item.WeekStart.Date == weekStart.Date
            && item.ConventionalThickness == thickness && item.Quality == quality);
        if (consumption is not null) return consumption;
        consumption = new PlannedConsumption
        {
            WeekStart = weekStart.Date, ConventionalThickness = thickness, Quality = quality
        };
        consumption.PropertyChanged += (_, _) => PlanningChanged?.Invoke(this, EventArgs.Empty);
        Consumptions.Add(consumption);
        return consumption;
    }
}
