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
    private readonly IPlannedConsumptionRepository _consumptionRepository;
    private readonly ObservableCollection<PlannedConsumption> _consumptions = [];
    public PlanningDataService(IPlannedArrivalRepository repository, IPlannedConsumptionRepository? consumptionRepository = null)
    {
        _repository = repository;
        _consumptionRepository = consumptionRepository ?? new SqlPlannedConsumptionRepository(SqlPersistenceRoot.ContextFactory);
        Arrivals = new(_arrivals); Consumptions = new(_consumptions);
    }

    public ReadOnlyObservableCollection<PlannedArrival> Arrivals { get; }
    public ReadOnlyObservableCollection<PlannedConsumption> Consumptions { get; }
    public event EventHandler? PlanningChanged;

    public bool IsAvailable { get; private set; }
    public bool ArrivalCommitCompleted { get; private set; }
    public void Invalidate() { IsAvailable = false; _arrivals.Clear(); _consumptions.Clear(); PlanningChanged?.Invoke(this, EventArgs.Empty); }
    public void Reload()
    {
        PlanningDiagnostics.Log("5 Reload START");
        IsAvailable = false; _arrivals.Clear(); _consumptions.Clear();
        try {
            var arrivals = _repository.GetAll();
            PlanningDiagnostics.Log("6 PlannedArrivals reload OK");
            var consumptions = _consumptionRepository.GetAll();
            PlanningDiagnostics.Log("7 PlannedConsumptions reload OK");
            foreach (var arrival in arrivals) _arrivals.Add(arrival);
            foreach (var consumption in consumptions) _consumptions.Add(consumption);
            IsAvailable = true;
        }
        finally { PlanningDiagnostics.Log("PlanningChanged START"); PlanningChanged?.Invoke(this, EventArgs.Empty); PlanningDiagnostics.Log("PlanningChanged END"); }
    }
    public PlannedArrival GetOrCreateArrival(Guid supplierId, DateTime date) =>
        _arrivals.FirstOrDefault(x => x.SupplierId == supplierId && x.Date.Date == date.Date)?.Copy()
        ?? new PlannedArrival { SupplierId = supplierId, Date = date.Date };
    public void Save(PlannedArrival arrival)
    {
        PlanningDiagnostics.Log($"1 SavePlannedArrival START Id={arrival.Id} Token={Convert.ToHexString(arrival.RowVersion)}\n{new System.Diagnostics.StackTrace(true)}");
        try { Execute(() => { PlanningDiagnostics.Log("2 repository Save"); _repository.Save(arrival); ArrivalCommitCompleted = true; }); }
        finally { ArrivalCommitCompleted = false; }
        PlanningDiagnostics.Log("SavePlannedArrival END");
    }
    public void Delete(PlannedArrival arrival)
    {
        try { Execute(() => { _repository.Delete(arrival); ArrivalCommitCompleted = true; }); }
        finally { ArrivalCommitCompleted = false; }
    }
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
        try { Reload(); } catch (Exception error) { throw new InvalidOperationException("Il salvataggio è riuscito ma non è stato possibile aggiornare la pagina. Premere Aggiorna da SQL.", error); }
    }

    public PlannedConsumption GetOrCreateConsumption(DateTime weekStart, decimal thickness, string quality)
    {
        var monday = weekStart.Date.AddDays(-(((int)weekStart.DayOfWeek + 6) % 7));
        return _consumptions.FirstOrDefault(x => x.WeekStart == monday && x.ConventionalThickness == thickness && x.Quality == quality)?.Copy()
            ?? new PlannedConsumption { WeekStart = monday, ConventionalThickness = thickness, Quality = quality };
    }

    public void Save(PlannedConsumption consumption) => Execute(() => {
        if (consumption.ExpectedCubicMeters == 0) _consumptionRepository.Delete(consumption);
        else _consumptionRepository.Save(consumption);
    });
    public void Delete(PlannedConsumption consumption) => Execute(() => _consumptionRepository.Delete(consumption));
}
