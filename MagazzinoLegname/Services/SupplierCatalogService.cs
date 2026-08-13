using System.Collections.ObjectModel;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class SupplierCatalogService
{
    private static readonly Lazy<SupplierCatalogService> SharedInstance = new(() => new SupplierCatalogService());
    private readonly ObservableCollection<SupplierPrice> _prices = [];

    private SupplierCatalogService()
    {
        var alpina = new Supplier(Guid.NewGuid(), "Segheria Alpina S.r.l.", true, "SEG");
        var nord = new Supplier(Guid.NewGuid(), "Legnami Nord S.p.A.", true, "LEG");
        var bosco = new Supplier(Guid.NewGuid(), "Bosco & Tavole S.r.l.", true, "BET");
        alpina.VatNumber = "IT02345670211"; alpina.Address = "Via delle Segherie 12"; alpina.PostalCode = "39100"; alpina.City = "Bolzano"; alpina.Province = "BZ"; alpina.Email = "ordini@segheria-alpina.demo";
        alpina.Contacts.Add(new SupplierContact { FirstName = "Luca", LastName = "Bernardi", Role = "Commerciale", Phone = "0471 000100", Mobile = "333 0000100", Email = "l.bernardi@segheria-alpina.demo" });
        nord.VatNumber = "IT04123450987"; nord.Address = "Via del Legno 8"; nord.PostalCode = "33170"; nord.City = "Pordenone"; nord.Province = "PN"; nord.Email = "ufficio@legnaminord.demo";
        nord.Contacts.Add(new SupplierContact { FirstName = "Sara", LastName = "Moretti", Role = "Logistica", Phone = "0434 000200", Email = "logistica@legnaminord.demo" });
        bosco.VatNumber = "IT01876540321"; bosco.Address = "Zona Industriale 4"; bosco.PostalCode = "32032"; bosco.City = "Feltre"; bosco.Province = "BL"; bosco.Email = "info@boscoetavole.demo";
        nord.ThicknessConfigurations[1].IsPlaningEnabled = false;
        nord.ThicknessConfigurations[2].IsPlaningEnabled = false;
        Suppliers = [alpina, nord, bosco];
        Seed(alpina, (23m, 425m, new DateTime(2025,1,1), new DateTime(2025,12,31)), (23m,445m,new DateTime(2026,1,1),null), (34m,478m,new DateTime(2026,1,1),null), (44m,512m,new DateTime(2026,1,1),null));
        Seed(nord, (23m,432m,new DateTime(2026,1,1),null), (34m,465m,new DateTime(2026,1,1),null), (44m,501m,new DateTime(2026,1,1),null));
        Seed(bosco, (23m,451m,new DateTime(2026,1,1),null), (34m,486m,new DateTime(2026,1,1),null), (44m,520m,new DateTime(2026,1,1),null));
        RefreshCurrentPrices(DateTime.Today);
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
        var supplier = new Supplier(Guid.NewGuid(), name.Trim(), true, CreateUniqueDraftCode());
        Suppliers.Add(supplier); NotifyChanged(); return supplier;
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
            old.ValidTo = start.AddDays(-1);
        }
        _prices.Add(new SupplierPrice { SupplierId=supplierId, ConventionalThickness=thickness, PricePerCubicMeter=price, ValidFrom=start });
        NotifyChanged();
    }
    private void RefreshCurrentPrices(DateTime date)
    {
        foreach (var supplier in Suppliers)
            foreach (var config in supplier.ThicknessConfigurations)
                config.CurrentPrice = GetValidPrice(supplier.Id, config.ConventionalThickness, date) ?? 0m;
    }
    private void Seed(Supplier supplier, params (decimal T, decimal P, DateTime From, DateTime? To)[] rows)
    {
        foreach (var row in rows) _prices.Add(new SupplierPrice { SupplierId=supplier.Id, ConventionalThickness=row.T, PricePerCubicMeter=row.P, ValidFrom=row.From, ValidTo=row.To });
    }
    private string CreateUniqueDraftCode()
    {
        var number = 1;
        string code;
        do code = $"NEW{number++}"; while (Suppliers.Any(s => s.Code == code));
        return code;
    }
}
