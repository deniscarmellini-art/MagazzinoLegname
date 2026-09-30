using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace MagazzinoLegname.Persistence.Repositories;

public interface IPlannedConsumptionRepository
{
    IReadOnlyList<PlannedConsumption> GetAll();
    void Save(PlannedConsumption consumption);
    void Delete(PlannedConsumption consumption);
}

public sealed class SqlPlannedConsumptionRepository(IDbContextFactory<MagazzinoDbContext> factory) : IPlannedConsumptionRepository
{
    public IReadOnlyList<PlannedConsumption> GetAll()
    {
        using var db = factory.CreateDbContext();
        return db.PlannedConsumptions.AsNoTracking().OrderBy(x => x.WeekStart)
            .ThenBy(x => x.ConventionalThickness).ThenBy(x => x.Quality).AsEnumerable()
            .Select(x => new PlannedConsumption { Id = x.Id, WeekStart = x.WeekStart,
                ConventionalThickness = x.ConventionalThickness, Quality = x.Quality,
                ExpectedCubicMeters = x.ExpectedCubicMeters, CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc, RowVersion = x.RowVersion.ToArray() }).ToArray();
    }

    public void Save(PlannedConsumption consumption)
    {
        if (consumption.WeekStart.DayOfWeek != DayOfWeek.Monday || consumption.Id == Guid.Empty || consumption.ConventionalThickness is not (23m or 34m or 44m)
            || consumption.Quality is not ("C" or "VISTA") || consumption.ExpectedCubicMeters <= 0
            || consumption.ExpectedCubicMeters >= 10000000000000000000m
            || decimal.Round(consumption.ExpectedCubicMeters, 9) != consumption.ExpectedCubicMeters)
            throw new InvalidOperationException("Indicare il lunedì della settimana, famiglia, qualità e m³ positivi con al massimo 9 decimali.");
        using var db = factory.CreateDbContext();
        PlannedConsumptionEntity entity;
        if (consumption.RowVersion.Length == 0)
        {
            entity = new() { Id = consumption.Id, WeekStart = consumption.WeekStart.Date,
                ConventionalThickness = consumption.ConventionalThickness, Quality = consumption.Quality,
                CreatedAtUtc = DateTime.UtcNow };
            db.PlannedConsumptions.Add(entity);
        }
        else
        {
            entity = db.PlannedConsumptions.SingleOrDefault(x => x.Id == consumption.Id)
                ?? throw new DbUpdateConcurrencyException("Fabbisogno eliminato da un'altra postazione.");
            if (!entity.RowVersion.SequenceEqual(consumption.RowVersion))
                throw new DbUpdateConcurrencyException("Fabbisogno modificato da un'altra postazione.");
            if (entity.WeekStart != consumption.WeekStart.Date || entity.ConventionalThickness != consumption.ConventionalThickness || entity.Quality != consumption.Quality)
                throw new InvalidOperationException("Per spostare il fabbisogno eliminare la cella precedente.");
            db.Entry(entity).Property(x => x.RowVersion).OriginalValue = consumption.RowVersion.ToArray();
        }
        entity.ExpectedCubicMeters = consumption.ExpectedCubicMeters;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        try { db.SaveChanges(); }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new DbUpdateConcurrencyException("La cella settimana/famiglia/qualità è già compilata da un'altra postazione.", error); }
    }

    public void Delete(PlannedConsumption consumption)
    {
        // An empty draft must never delete another client's newly inserted cell.
        if (consumption.RowVersion.Length == 0) return;
        using var db = factory.CreateDbContext();
        var entity = db.PlannedConsumptions.SingleOrDefault(x => x.Id == consumption.Id);
        if (entity is null || !entity.RowVersion.SequenceEqual(consumption.RowVersion))
            throw new DbUpdateConcurrencyException("Fabbisogno modificato o eliminato da un'altra postazione.");
        db.Entry(entity).Property(x => x.RowVersion).OriginalValue = consumption.RowVersion.ToArray();
        db.PlannedConsumptions.Remove(entity);
        db.SaveChanges();
    }
}
