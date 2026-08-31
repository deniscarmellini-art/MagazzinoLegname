namespace MagazzinoLegname.Models;

public sealed record PackageExitResult(
    string PackageCode,
    PackageType PackageType,
    DateTime Date,
    string Operator,
    decimal? CubicMeters,
    string Message);