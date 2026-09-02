using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public enum ConsumableStockStatus { Ok, ToOrder, BelowMinimumOrdered, Ordered, ToVerify }
public enum ConsumableOrderStatus { None, Ordered, PartiallyReceived, Received, Cancelled }
public enum ConsumptionPeriod { Unspecified, Day, Week, Month, Year }

public sealed class ConsumableItem : ObservableObject
{
    private string _internalCode = string.Empty;
    private string _productName = string.Empty;
    private string _supplierName = string.Empty;
    private string _department = string.Empty;
    private string _unitOfMeasure = string.Empty;
    private decimal? _quantityPerUnit;
    private decimal? _minimumStock;
    private string _consumptionAverageText = string.Empty;
    private decimal? _consumptionAverageQuantity;
    private ConsumptionPeriod _consumptionPeriod;
    private int? _leadTimeDays;
    private string _leadTimeText = string.Empty;
    private string _packaging = string.Empty;
    private string _notes = string.Empty;
    private string _legacyExtraNote = string.Empty;
    private string _legacyOrderText = string.Empty;
    private string _legacySupplierName = string.Empty;
    private string _legacyDepartment = string.Empty;
    private bool _needsVerification;
    private string? _photoPath;
    private bool _isActive = true;

    public Guid Id { get; init; } = Guid.NewGuid();
    public string InternalCode { get => _internalCode; set => SetProperty(ref _internalCode, value); }
    public string ProductName { get => _productName; set => SetProperty(ref _productName, value); }
    public string SupplierName { get => _supplierName; set => SetProperty(ref _supplierName, value); }
    public string Department { get => _department; set => SetProperty(ref _department, value); }
    public string UnitOfMeasure { get => _unitOfMeasure; set => SetProperty(ref _unitOfMeasure, value); }
    public decimal? QuantityPerUnit { get => _quantityPerUnit; set => SetProperty(ref _quantityPerUnit, value); }
    public decimal? MinimumStock { get => _minimumStock; set => SetProperty(ref _minimumStock, value); }
    public string ConsumptionAverageText { get => _consumptionAverageText; set => SetProperty(ref _consumptionAverageText, value); }
    public decimal? ConsumptionAverageQuantity { get => _consumptionAverageQuantity; set => SetProperty(ref _consumptionAverageQuantity, value); }
    public ConsumptionPeriod ConsumptionPeriod { get => _consumptionPeriod; set => SetProperty(ref _consumptionPeriod, value); }
    public int? LeadTimeDays { get => _leadTimeDays; set => SetProperty(ref _leadTimeDays, value); }
    public string LeadTimeText { get => _leadTimeText; set => SetProperty(ref _leadTimeText, value); }
    public string Packaging { get => _packaging; set => SetProperty(ref _packaging, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
    public string LegacyExtraNote { get => _legacyExtraNote; set => SetProperty(ref _legacyExtraNote, value); }
    public string LegacyOrderText { get => _legacyOrderText; set => SetProperty(ref _legacyOrderText, value); }
    public string LegacySupplierName { get => _legacySupplierName; set => SetProperty(ref _legacySupplierName, value); }
    public string LegacyDepartment { get => _legacyDepartment; set => SetProperty(ref _legacyDepartment, value); }
    public bool NeedsVerification { get => _needsVerification; set { if (SetProperty(ref _needsVerification, value)) OnPropertyChanged(nameof(StatusDisplay)); } }
    public string? PhotoPath { get => _photoPath; set => SetProperty(ref _photoPath, value); }
    public bool IsActive { get => _isActive; set { if (SetProperty(ref _isActive, value)) { OnPropertyChanged(nameof(StatusDisplay)); OnPropertyChanged(nameof(ToggleActionLabel)); } } }
    public string StatusDisplay => IsActive ? NeedsVerification ? "Da verificare" : "Attivo" : "Disattivato";
    public string ToggleActionLabel => IsActive ? "Disattiva" : "Riattiva";
}

public sealed record ConsumableInventoryReading
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid MaterialId { get; init; }
    public required DateTime ReadingDate { get; init; }
    public required decimal Quantity { get; init; }
    public decimal? CountedUnits { get; init; }
    public decimal? QuantityPerUnitSnapshot { get; init; }
    public decimal CalculatedQuantity => Quantity;
    public string? Operator { get; init; }
    public string Note { get; init; } = string.Empty;
    public string Origin { get; init; } = "Inventario";
}

public sealed class ConsumableOrderInfo : ObservableObject
{
    private decimal? _quantity;
    private DateTime? _orderDate;
    private DateTime? _expectedDeliveryDate;
    private string _note = string.Empty;
    private ConsumableOrderStatus _status;
    public Guid MaterialId { get; init; }
    public decimal? Quantity { get => _quantity; set => SetProperty(ref _quantity, value); }
    public DateTime? OrderDate { get => _orderDate; set => SetProperty(ref _orderDate, value); }
    public DateTime? ExpectedDeliveryDate { get => _expectedDeliveryDate; set => SetProperty(ref _expectedDeliveryDate, value); }
    public string Note { get => _note; set => SetProperty(ref _note, value); }
    public ConsumableOrderStatus Status { get => _status; set => SetProperty(ref _status, value); }
    public bool IsOpen => Status is ConsumableOrderStatus.Ordered or ConsumableOrderStatus.PartiallyReceived;
}
