using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Persistence.Repositories;

public interface ISupplierRepository
{
    IReadOnlyList<Supplier> GetAll();
    void Save(Supplier supplier);
    IReadOnlyList<SupplierPrice> GetPrices(Guid supplierId);
    void AddPrice(Guid supplierId, decimal thickness, decimal price, DateTime validFrom);
}

public sealed class SqlSupplierRepository(IDbContextFactory<MagazzinoDbContext> contextFactory) : ISupplierRepository
{
    public IReadOnlyList<Supplier> GetAll()
    {
        using var context = contextFactory.CreateDbContext();
        return context.Suppliers.AsNoTracking().Include(x => x.Contacts).Include(x => x.ThicknessConfigurations)
            .OrderBy(x => x.Name).AsEnumerable().Select(Map).ToList();
    }

    public void Save(Supplier supplier)
    {
        PersistenceDebugLog.Write($"SqlSupplierRepository.Save: Id={supplier.Id}, Code='{supplier.Code}', Name='{supplier.Name}', IsActive={supplier.IsActive}, Configurations={supplier.ThicknessConfigurations.Count}, Contacts={supplier.Contacts.Count}");
        try
        {
            using var context = contextFactory.CreateDbContext();
            var entity = context.Suppliers.Include(x => x.Contacts).Include(x => x.ThicknessConfigurations).SingleOrDefault(x => x.Id == supplier.Id);
            var isInsert = entity is null;
            if (isInsert)
            {
                if (supplier.RowVersion.Length > 0)
                    throw new InvalidOperationException("Il fornitore non esiste più. Ricaricare la pagina.");
                entity = new SupplierEntity { Id = supplier.Id };
                context.Suppliers.Add(entity);
                PersistenceDebugLog.Write("SqlSupplierRepository.Save: nuovo fornitore, esecuzione INSERT.");
            }
            else
            {
                EnsureCurrent(supplier.RowVersion, entity!.RowVersion);
                PersistenceDebugLog.Write("SqlSupplierRepository.Save: fornitore esistente, esecuzione UPDATE.");
            }

            entity.Code = supplier.Code.Trim().ToUpperInvariant(); entity.Name = supplier.Name.Trim(); entity.IsActive = supplier.IsActive;
            entity.VatNumber = NullIfEmpty(supplier.VatNumber); entity.TaxCode = NullIfEmpty(supplier.TaxCode); entity.Address = NullIfEmpty(supplier.Address);
            entity.PostalCode = NullIfEmpty(supplier.PostalCode); entity.City = NullIfEmpty(supplier.City); entity.Province = NullIfEmpty(supplier.Province);
            entity.Country = NullIfEmpty(supplier.Country); entity.Email = NullIfEmpty(supplier.Email); entity.CertifiedEmail = NullIfEmpty(supplier.CertifiedEmail);

            var meaningfulContacts = supplier.Contacts.Where(IsMeaningful).ToList();
            var retainedContactIds = meaningfulContacts.Select(x => x.Id).ToHashSet();
            context.SupplierContacts.RemoveRange(entity.Contacts.Where(x => !retainedContactIds.Contains(x.Id)).ToList());
            foreach (var contact in meaningfulContacts)
            {
                var target = entity.Contacts.SingleOrDefault(x => x.Id == contact.Id);
                if (target is null) { target = new SupplierContactEntity { Id = contact.Id, SupplierId = supplier.Id }; entity.Contacts.Add(target); }
                target.FirstName = contact.FirstName.Trim(); target.LastName = contact.LastName.Trim(); target.Role = NullIfEmpty(contact.Role);
                target.Phone = NullIfEmpty(contact.Phone); target.Mobile = NullIfEmpty(contact.Mobile); target.Email = NullIfEmpty(contact.Email);
            }

            foreach (var configuration in supplier.ThicknessConfigurations)
            {
                if (configuration.ConventionalThickness is not (23m or 34m or 44m))
                    throw new InvalidOperationException($"Configurazione spessore {configuration.ConventionalThickness} non valida.");
                var target = entity.ThicknessConfigurations.SingleOrDefault(x => x.ConventionalThickness == configuration.ConventionalThickness);
                if (target is null)
                {
                    target = new SupplierThicknessConfigurationEntity { Id = configuration.PersistenceId == Guid.Empty ? Guid.NewGuid() : configuration.PersistenceId, SupplierId = supplier.Id, ConventionalThickness = configuration.ConventionalThickness };
                    entity.ThicknessConfigurations.Add(target);
                }
                else
                    EnsureCurrent(configuration.RowVersion, target.RowVersion);
                target.IsPlaningEnabled = configuration.IsPlaningEnabled;
                target.PlaningReductionMillimeters = configuration.EffectivePlaningReductionMillimeters;
            }

            PersistenceDebugLog.Write("SqlSupplierRepository.Save: chiamata SaveChanges.");
            var affectedRows = context.SaveChanges();
            PersistenceDebugLog.Write($"SqlSupplierRepository.Save: SaveChanges completato, righe interessate={affectedRows}.");
            supplier.RowVersion = entity.RowVersion;
            foreach (var configuration in supplier.ThicknessConfigurations)
            {
                var saved = entity.ThicknessConfigurations.Single(x => x.ConventionalThickness == configuration.ConventionalThickness);
                configuration.PersistenceId = saved.Id; configuration.RowVersion = saved.RowVersion;
            }
        }
        catch (Exception exception)
        {
            PersistenceDebugLog.WriteException("SqlSupplierRepository.Save", exception);
            throw;
        }
    }

    public IReadOnlyList<SupplierPrice> GetPrices(Guid supplierId)
    {
        using var context = contextFactory.CreateDbContext();
        return context.SupplierPrices.AsNoTracking().Where(x => x.SupplierId == supplierId).OrderBy(x => x.ConventionalThickness).ThenByDescending(x => x.ValidFrom)
            .Select(x => new SupplierPrice { Id = x.Id, SupplierId = x.SupplierId, ConventionalThickness = x.ConventionalThickness, PricePerCubicMeter = x.PricePerCubicMeter, ValidFrom = x.ValidFrom, ValidTo = x.ValidTo }).ToList();
    }

    public void AddPrice(Guid supplierId, decimal thickness, decimal price, DateTime validFrom)
    {
        using var strategyContext = contextFactory.CreateDbContext();
        var strategy = strategyContext.Database.CreateExecutionStrategy();
        strategy.Execute(() =>
        {
            using var context = contextFactory.CreateDbContext();
            using var transaction = context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var start = validFrom.Date;
            var current = context.SupplierPrices.Where(x => x.SupplierId == supplierId && x.ConventionalThickness == thickness && x.ValidFrom <= start && (x.ValidTo == null || x.ValidTo >= start)).ToList();
            foreach (var old in current)
            {
                if (old.ValidFrom.Date >= start) throw new InvalidOperationException("Esiste già un prezzo con decorrenza uguale o successiva.");
                old.ValidTo = start.AddDays(-1);
            }
            context.SupplierPrices.Add(new SupplierPriceEntity { Id = Guid.NewGuid(), SupplierId = supplierId, ConventionalThickness = thickness, PricePerCubicMeter = price, ValidFrom = start });
            context.SaveChanges(); transaction.Commit();
        });
    }

    private static Supplier Map(SupplierEntity entity)
    {
        var supplier = new Supplier(entity.Id, entity.Name, entity.IsActive, entity.Code) { RowVersion = entity.RowVersion, VatNumber = entity.VatNumber ?? "", TaxCode = entity.TaxCode ?? "", Address = entity.Address ?? "", PostalCode = entity.PostalCode ?? "", City = entity.City ?? "", Province = entity.Province ?? "", Country = entity.Country ?? "Italia", Email = entity.Email ?? "", CertifiedEmail = entity.CertifiedEmail ?? "" };
        supplier.Contacts.Clear(); foreach (var x in entity.Contacts.OrderBy(x => x.LastName).ThenBy(x => x.FirstName)) supplier.Contacts.Add(new SupplierContact { Id = x.Id, FirstName = x.FirstName, LastName = x.LastName, Role = x.Role ?? "", Phone = x.Phone ?? "", Mobile = x.Mobile ?? "", Email = x.Email ?? "" });
        supplier.ThicknessConfigurations.Clear(); foreach (var x in entity.ThicknessConfigurations.OrderBy(x => x.ConventionalThickness)) supplier.ThicknessConfigurations.Add(new SupplierThicknessConfiguration(entity.Id, x.ConventionalThickness, x.IsPlaningEnabled, x.PlaningReductionMillimeters) { PersistenceId = x.Id, RowVersion = x.RowVersion });
        return supplier;
    }
    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsMeaningful(SupplierContact contact) =>
        !string.IsNullOrWhiteSpace(contact.FirstName) || !string.IsNullOrWhiteSpace(contact.LastName) ||
        !string.IsNullOrWhiteSpace(contact.Role) || !string.IsNullOrWhiteSpace(contact.Phone) ||
        !string.IsNullOrWhiteSpace(contact.Mobile) || !string.IsNullOrWhiteSpace(contact.Email);
    private static void EnsureCurrent(byte[] expected, byte[] actual) { if (expected.Length > 0 && !expected.SequenceEqual(actual)) throw new DbUpdateConcurrencyException("I dati sono stati modificati da un'altra postazione."); }
}

public interface IOperatorRepository { IReadOnlyList<Operator> GetAll(); Operator Add(); void Save(Operator item); }
public sealed class SqlOperatorRepository(IDbContextFactory<MagazzinoDbContext> contextFactory) : IOperatorRepository
{
    public IReadOnlyList<Operator> GetAll() { using var db = contextFactory.CreateDbContext(); return db.Operators.AsNoTracking().OrderBy(x => x.LastName).ThenBy(x => x.FirstName).AsEnumerable().Select(x => new Operator { Id=x.Id, FirstName=x.FirstName, LastName=x.LastName, IsActive=x.IsActive, RowVersion=x.RowVersion }).ToList(); }
    public Operator Add() { using var db=contextFactory.CreateDbContext(); var entity=new OperatorEntity { Id=Guid.NewGuid(), FirstName="Nuovo", LastName="Operatore", IsActive=true }; db.Add(entity); db.SaveChanges(); return new Operator { Id=entity.Id, FirstName=entity.FirstName, LastName=entity.LastName, IsActive=entity.IsActive, RowVersion=entity.RowVersion }; }
    public void Save(Operator item) { using var db=contextFactory.CreateDbContext(); var entity=db.Operators.SingleOrDefault(x=>x.Id==item.Id) ?? throw new InvalidOperationException("L'operatore non esiste più. Ricaricare la pagina."); if(item.RowVersion.Length>0&&!item.RowVersion.SequenceEqual(entity.RowVersion)) throw new DbUpdateConcurrencyException(); entity.FirstName=item.FirstName.Trim(); entity.LastName=item.LastName.Trim(); entity.IsActive=item.IsActive; db.SaveChanges(); item.RowVersion=entity.RowVersion; }
}

public sealed record DomainConfigurationSnapshot(IReadOnlyList<ThicknessFamilyConfiguration> Families, GeneralSettings General, PlanningSettings Planning);
public interface IDomainConfigurationRepository { DomainConfigurationSnapshot LoadOrInitialize(); void Save(MaterialParameters material, GeneralSettings general, PlanningSettings planning); }
public sealed class SqlDomainConfigurationRepository(IDbContextFactory<MagazzinoDbContext> contextFactory) : IDomainConfigurationRepository
{
    public DomainConfigurationSnapshot LoadOrInitialize()
    {
        using (var check = contextFactory.CreateDbContext())
        {
            if (!check.ThicknessFamilies.Any() || !check.ApplicationSettings.Any()) InitializeMissingConfigurations(check);
        }
        using var db=contextFactory.CreateDbContext();
        var app=db.ApplicationSettings.AsNoTracking().Single(x=>x.Id==1);
        var families=db.ThicknessFamilies.AsNoTracking().OrderBy(x=>x.ConventionalThickness).AsEnumerable().Select(x=>new ThicknessFamilyConfiguration(x.MinimumIncomingThickness,x.MaximumIncomingThickness,x.ConventionalThickness){PersistenceId=x.Id,RowVersion=x.RowVersion}).ToList();
        return new(families,new GeneralSettings { DefaultTimberCertification=app.DefaultTimberCertification, RowVersion=app.RowVersion },new PlanningSettings { StandardCubicMetersPerExpectedLoad23=app.StandardCubicMetersPerExpectedLoad23,StandardCubicMetersPerExpectedLoad34=app.StandardCubicMetersPerExpectedLoad34,StandardCubicMetersPerExpectedLoad44=app.StandardCubicMetersPerExpectedLoad44,RowVersion=app.RowVersion });
    }
    private void InitializeMissingConfigurations(MagazzinoDbContext strategyContext)
    {
        var strategy = strategyContext.Database.CreateExecutionStrategy();
        strategy.Execute(() =>
        {
            using var db = contextFactory.CreateDbContext();
            using var transaction = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
            if (!db.ThicknessFamilies.Any()) foreach (var item in Defaults()) db.ThicknessFamilies.Add(item);
            if (!db.ApplicationSettings.Any()) db.ApplicationSettings.Add(new ApplicationSettingsEntity { Id=1, DefaultTimberCertification="PEFC", StandardCubicMetersPerExpectedLoad23=50m, StandardCubicMetersPerExpectedLoad34=50m, StandardCubicMetersPerExpectedLoad44=50m });
            db.SaveChanges();
            transaction.Commit();
        });
    }
    public void Save(MaterialParameters material, GeneralSettings general, PlanningSettings planning)
    {
        using var db=contextFactory.CreateDbContext(); var app=db.ApplicationSettings.Single(x=>x.Id==1);
        var expected=general.RowVersion.Length>0?general.RowVersion:planning.RowVersion; if(expected.Length>0&&!expected.SequenceEqual(app.RowVersion)) throw new DbUpdateConcurrencyException();
        app.DefaultTimberCertification=general.DefaultTimberCertification; app.StandardCubicMetersPerExpectedLoad23=planning.StandardCubicMetersPerExpectedLoad23; app.StandardCubicMetersPerExpectedLoad34=planning.StandardCubicMetersPerExpectedLoad34; app.StandardCubicMetersPerExpectedLoad44=planning.StandardCubicMetersPerExpectedLoad44;
        foreach(var item in material.ThicknessFamilies){var entity=db.ThicknessFamilies.Single(x=>x.Id==item.PersistenceId); if(item.RowVersion.Length>0&&!item.RowVersion.SequenceEqual(entity.RowVersion)) throw new DbUpdateConcurrencyException(); entity.MinimumIncomingThickness=item.MinimumIncomingThickness;entity.MaximumIncomingThickness=item.MaximumIncomingThickness;entity.ConventionalThickness=item.ConventionalThickness;}
        db.SaveChanges(); general.RowVersion=app.RowVersion;planning.RowVersion=app.RowVersion; foreach(var item in material.ThicknessFamilies)item.RowVersion=db.ThicknessFamilies.Local.Single(x=>x.Id==item.PersistenceId).RowVersion;
    }
    private static IEnumerable<ThicknessFamilyEntity> Defaults(){yield return New(20,29,23);yield return New(30,39,34);yield return New(40,49,44);}
    private static ThicknessFamilyEntity New(decimal min,decimal max,decimal conventional)=>new(){Id=Guid.NewGuid(),MinimumIncomingThickness=min,MaximumIncomingThickness=max,ConventionalThickness=conventional};
}
