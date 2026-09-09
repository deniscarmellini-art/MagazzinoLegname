using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;

namespace MagazzinoLegname.Services;

internal sealed class SqlDomainConfigurationState
{
    private static readonly Lazy<SqlDomainConfigurationState> SharedState = new(() => new());
    public static SqlDomainConfigurationState Shared => SharedState.Value;
    public MaterialParameters Material { get; } = new();
    public GeneralSettings General { get; } = new();
    public PlanningSettings Planning { get; } = new();

    private SqlDomainConfigurationState() { }
    public static void Initialize() => Shared.Reload();

    public void Reload()
    {
        try
        {
            var snapshot = SqlPersistenceRoot.DomainConfigurations.LoadOrInitialize();
            Material.ThicknessFamilies.Clear(); foreach (var family in snapshot.Families) Material.ThicknessFamilies.Add(family);
            General.DefaultTimberCertification = snapshot.General.DefaultTimberCertification; General.RowVersion = snapshot.General.RowVersion;
            Planning.StandardCubicMetersPerExpectedLoad23 = snapshot.Planning.StandardCubicMetersPerExpectedLoad23;
            Planning.StandardCubicMetersPerExpectedLoad34 = snapshot.Planning.StandardCubicMetersPerExpectedLoad34;
            Planning.StandardCubicMetersPerExpectedLoad44 = snapshot.Planning.StandardCubicMetersPerExpectedLoad44;
            Planning.RowVersion = snapshot.Planning.RowVersion;
        }
        catch (Exception exception) { throw SqlPersistenceRoot.OperatorException(exception); }
    }

    public void Save()
    {
        try { SqlPersistenceRoot.DomainConfigurations.Save(Material, General, Planning); }
        catch (Exception exception)
        {
            if (DatabaseErrorTranslator.Translate(exception).Kind == DatabaseFailureKind.ConcurrencyConflict) Reload();
            throw SqlPersistenceRoot.OperatorException(exception);
        }
    }
}
