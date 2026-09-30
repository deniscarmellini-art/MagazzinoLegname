using System.Data;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace MagazzinoLegname.Persistence.Repositories;
public interface IPlannedArrivalRepository
{
 IReadOnlyList<PlannedArrival> GetAll();
 void Save(PlannedArrival arrival);
 void Delete(PlannedArrival arrival);
}
public sealed class SqlPlannedArrivalRepository(IDbContextFactory<MagazzinoDbContext> factory) : IPlannedArrivalRepository
{
 public IReadOnlyList<PlannedArrival> GetAll()
 {
  using var db = factory.CreateDbContext();
  return db.PlannedArrivals.AsNoTracking().OrderBy(x => x.Date).ThenBy(x => x.SupplierNameSnapshot).AsEnumerable().Select(Map).ToArray();
 }
 public void Save(PlannedArrival a)
 {
  if (a.Id == Guid.Empty || a.SupplierId == Guid.Empty || a.LoadQuantity <= 0 || a.ConventionalThickness is not (23m or 34m or 44m) || a.Quality is not ("C" or "VISTA") || a.Notes.Length > 2000)
   throw new InvalidOperationException("Indicare fornitore, famiglia, qualità e un numero positivo di carichi.");
  using var strategy = factory.CreateDbContext();
  try { strategy.Database.CreateExecutionStrategy().Execute(() => {
   using var db = factory.CreateDbContext(); using var tx = db.Database.BeginTransaction(IsolationLevel.Serializable);
   var x = db.PlannedArrivals.SingleOrDefault(x => x.Id == a.Id);
   if (a.RowVersion.Length == 0)
   {
    if (x is not null || db.PlannedArrivals.Any(x => x.SupplierId == a.SupplierId && x.Date == a.Date.Date)) throw new DbUpdateConcurrencyException("Cella fornitore/giorno già compilata.");
    x = new PlannedArrivalEntity { Id = a.Id, Date = a.Date.Date, SupplierId = a.SupplierId, CreatedAtUtc = DateTime.UtcNow };
    db.PlannedArrivals.Add(x);
   }
   else
   {
    if (x is null || !x.RowVersion.SequenceEqual(a.RowVersion)) throw new DbUpdateConcurrencyException("Pianificazione modificata o eliminata da un altro client.");
    if (x.Status == PlannedArrivalStatus.Confirmed) throw new InvalidOperationException("Arrivo confermato in sola lettura.");
    if (x.SupplierId != a.SupplierId || x.Date != a.Date.Date) throw new InvalidOperationException("Per spostare l'arrivo, eliminare la vecchia pianificazione e crearne una nuova.");
    db.Entry(x).Property(x => x.RowVersion).OriginalValue = a.RowVersion.ToArray();
   }
   var supplier = db.Suppliers.SingleOrDefault(x => x.Id == a.SupplierId) ?? throw new InvalidOperationException("Fornitore non disponibile.");
   if (!supplier.IsActive && a.Status != PlannedArrivalStatus.Confirmed) throw new InvalidOperationException("Fornitore inattivo: nuove pianificazioni e modifiche non consentite.");
   if (!Enum.IsDefined(a.Status) || a.ConfirmedBy?.Length > 201) throw new InvalidOperationException("Stato pianificazione non valido.");
   var settings = db.ApplicationSettings.Single();
   var standard = a.ConventionalThickness switch { 23m => settings.StandardCubicMetersPerExpectedLoad23, 34m => settings.StandardCubicMetersPerExpectedLoad34, _ => settings.StandardCubicMetersPerExpectedLoad44 };
   var volume = checked(standard * a.LoadQuantity);
   if (a.Status == PlannedArrivalStatus.Expected)
   {
    if (volume <= 0 || volume >= 10000000000000000000m) throw new InvalidOperationException("MC standard non validi o quantità fuori limite.");
    x.ExpectedCubicMeters = volume;
   }
   else if (a.RowVersion.Length == 0) throw new InvalidOperationException("Creare prima l'arrivo previsto.");
   x.SupplierNameSnapshot = supplier.Name; x.ConventionalThickness = a.ConventionalThickness.Value; x.Quality = a.Quality;
   x.LoadQuantity = a.LoadQuantity; x.Notes = a.Notes.Trim(); x.UpdatedAtUtc = DateTime.UtcNow;
   x.Status = a.Status; x.ConfirmedAt = a.ConfirmedAt; x.ConfirmedBy = a.ConfirmedBy;
   db.SaveChanges(); MagazzinoLegname.Services.PlanningDiagnostics.Log("3 SaveChanges OK");
   tx.Commit(); MagazzinoLegname.Services.PlanningDiagnostics.Log("4 transaction COMMIT OK");
  }); }
  catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 }) { throw new DbUpdateConcurrencyException("Esiste già una pianificazione per questo fornitore/giorno.", ex); }
 }
 public void Delete(PlannedArrival a)
 {
  if (a.RowVersion.Length == 0) return;
  using var db = factory.CreateDbContext(); var x = db.PlannedArrivals.SingleOrDefault(x => x.Id == a.Id);
  if (x is null || !x.RowVersion.SequenceEqual(a.RowVersion)) throw new DbUpdateConcurrencyException("Pianificazione modificata o eliminata da un altro client.");
  if (x.Status == PlannedArrivalStatus.Confirmed) throw new InvalidOperationException("Arrivo confermato non eliminabile.");
  db.Entry(x).Property(x => x.RowVersion).OriginalValue = a.RowVersion.ToArray(); db.PlannedArrivals.Remove(x); db.SaveChanges();
 }
 private static PlannedArrival Map(PlannedArrivalEntity x)
 {
  var a = new PlannedArrival { Id = x.Id, Date = x.Date, SupplierId = x.SupplierId, SupplierNameSnapshot = x.SupplierNameSnapshot,
   ConventionalThickness = x.ConventionalThickness, Quality = x.Quality, LoadQuantity = x.LoadQuantity, ExpectedCubicMeters = x.ExpectedCubicMeters,
   Notes = x.Notes, CreatedAtUtc = x.CreatedAtUtc, UpdatedAtUtc = x.UpdatedAtUtc, RowVersion = x.RowVersion.ToArray() };
  if (x.Status == PlannedArrivalStatus.Confirmed) a.Confirm(x.ConfirmedAt!.Value, x.ConfirmedBy); return a;
 }
}
