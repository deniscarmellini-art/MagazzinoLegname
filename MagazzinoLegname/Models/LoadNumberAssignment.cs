namespace MagazzinoLegname.Models;

public sealed record LoadNumberAssignment(Guid SupplierId, int Year, int AnnualSequence)
{
    public string LoadNumber => $"{AnnualSequence}-{Year % 100:00}";
}
