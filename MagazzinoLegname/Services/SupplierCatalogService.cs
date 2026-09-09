using System.Collections.ObjectModel;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Repositories;

namespace MagazzinoLegname.Services;

public sealed class SupplierCatalogService
{
    private static readonly Lazy<SupplierCatalogService> SharedInstance = new(() => new SupplierCatalogService());
    private readonly ObservableCollection<SupplierPrice> _prices = [];
    private readonly ISupplierRepository _repository = SqlPersistenceRoot.Suppliers;
    private SupplierCatalogService()
    {
        Suppliers = [];
        Reload();
    }

    public static SupplierCatalogService Shared => SharedInstance.Value;
    public ObservableCollection<Supplier> Suppliers { get; }
    public event EventHandler? CatalogChanged;

    public SupplierThicknessConfiguration? GetConfiguration(Guid supplierId, decimal thickness) =>
        Suppliers.FirstOrDefault(s => s.Id == supplierId)?.ThicknessConfigurations
            .FirstOrDefault(c => c.ConventionalThickness == thickness);
    public decimal? GetValidPrice(Guid supplierId, decimal thickness, DateTime date) =>
        GetHistory(supplierId, thickness).Where(p => p.IsValidOn(date)).OrderByDescending(p => p.ValidFrom)
            .Select(p => (decimal?)p.PricePerCubicMeter).FirstOrDefault();
    public IReadOnlyList<SupplierPrice> GetHistory(Guid supplierId, decimal? thickness = null) =>
        _prices.Where(p => p.SupplierId == supplierId && (!thickness.HasValue || p.ConventionalThickness == thickness))
            .OrderBy(p => p.ConventionalThickness).ThenByDescending(p => p.ValidFrom).ToList();
    public Supplier AddSupplier(string name)
    {
        var supplier = new Supplier(Guid.NewGuid(), name, true, CreateUniqueDraftCode());
        Suppliers.Add(supplier);
        NotifyChanged();
        PersistenceDebugLog.Write($"SupplierCatalogService.AddSupplier: creato modello non persistito Id={supplier.Id}, Code='{supplier.Code}', Name='{supplier.Name}'.");
        return supplier;
    }
    public void SaveSupplier(Supplier supplier)
    {
        PersistenceDebugLog.Write($"SupplierCatalogService.SaveSupplier: chiamata repository Id={supplier.Id}, Code='{supplier.Code}', Name='{supplier.Name}', IsActive={supplier.IsActive}, Configurations={supplier.ThicknessConfigurations.Count}, Contacts={supplier.Contacts.Count}, Prices={GetHistory(supplier.Id).Count}.");
        try { _repository.Save(supplier); ReloadPrices(); NotifyChanged(); }
        catch (Exception exception) { PersistenceDebugLog.WriteException("SupplierCatalogService.SaveSupplier", exception); if (DatabaseErrorTranslator.Translate(exception).Kind == DatabaseFailureKind.ConcurrencyConflict) Reload(); throw SqlPersistenceRoot.OperatorException(exception); }
    }
    public void Reload()
    {
        try { Suppliers.Clear(); foreach (var supplier in _repository.GetAll()) Suppliers.Add(supplier); ReloadPrices(); NotifyChanged(); }
        catch (Exception exception) { throw SqlPersistenceRoot.OperatorException(exception); }
    }
    public void NotifyChanged() { RefreshCurrentPrices(DateTime.Today); CatalogChanged?.Invoke(this, EventArgs.Empty); }
    public bool IsSupplierCodeUnique(Supplier supplier) => !string.IsNullOrWhiteSpace(supplier.Code)
        && Suppliers.Count(item => string.Equals(item.Code, supplier.Code, StringComparison.OrdinalIgnoreCase)) == 1;
    public void AddPrice(Guid supplierId, decimal thickness, decimal price, DateTime validFrom)
    {
        if (thickness is not (23m or 34m or 44m)) throw new ArgumentOutOfRangeException(nameof(thickness));
        if (price <= 0m) throw new ArgumentOutOfRangeException(nameof(price), "Il prezzo deve essere maggiore di zero.");
        var start = validFrom.Date;
        foreach (var old in _prices.Where(p => p.SupplierId == supplierId && p.ConventionalThickness == thickness && p.IsValidOn(start)).ToList())
        {
            if (old.ValidFrom.Date >= start) throw new InvalidOperationException("Esiste già un prezzo con decorrenza uguale o successiva.");
        }
        try { _repository.AddPrice(supplierId, thickness, price, start); ReloadPrices(); NotifyChanged(); }
        catch (Exception exception) { throw SqlPersistenceRoot.OperatorException(exception); }
    }
    private void RefreshCurrentPrices(DateTime date)
    {
        foreach (var supplier in Suppliers)
            foreach (var config in supplier.ThicknessConfigurations)
                config.CurrentPrice = GetValidPrice(supplier.Id, config.ConventionalThickness, date) ?? 0m;
    }
    private string CreateUniqueDraftCode()
    {
        var number = 1;
        string code;
        do code = $"NEW{number++}"; while (Suppliers.Any(s => s.Code == code));
        return code;
    }
    private void ReloadPrices()
    {
        _prices.Clear();
        foreach (var supplier in Suppliers) foreach (var price in _repository.GetPrices(supplier.Id)) _prices.Add(price);
    }
}
