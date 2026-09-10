using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Persistence.Repositories;

public sealed record PersistedWasteAdjustment(WasteAdjustment Adjustment, byte[] MaterialGroupRowVersion);

public interface IWasteAdjustmentRepository
{
    PersistedWasteAdjustment Add(Guid loadId, Guid materialGroupId, byte[] materialGroupRowVersion,
        WasteAdjustment adjustment, Operator operatorItem);
}

public sealed class SqlWasteAdjustmentRepository(IDbContextFactory<MagazzinoDbContext> contextFactory)
    : IWasteAdjustmentRepository
{
    public PersistedWasteAdjustment Add(Guid loadId, Guid materialGroupId, byte[] materialGroupRowVersion,
        WasteAdjustment adjustment, Operator operatorItem)
    {
        using var db = contextFactory.CreateDbContext();
        var group = db.MaterialGroups.SingleOrDefault(x => x.Id == materialGroupId && x.LoadId == loadId)
            ?? throw new InvalidOperationException("Il gruppo materiale non è più presente nel database.");
        if (!group.IsClassified)
            throw new InvalidOperationException("Il gruppo deve essere classificato prima della Rettifica scarti.");
        if (group.WasteVerified || db.WasteAdjustments.Any(x => x.MaterialGroupId == materialGroupId))
            throw new InvalidOperationException("La Rettifica scarti del gruppo è già stata registrata.");
        if (!db.Operators.Any(x => x.Id == operatorItem.Id && x.IsActive))
            throw new InvalidOperationException("L'operatore selezionato non è più disponibile nel database.");

        if (materialGroupRowVersion.Length > 0)
            db.Entry(group).Property(x => x.RowVersion).OriginalValue = materialGroupRowVersion.ToArray();
        group.WasteVerified = true;

        var entity = new WasteAdjustmentEntity
        {
            Id = adjustment.AdjustmentId,
            LoadId = loadId,
            MaterialGroupId = materialGroupId,
            OccurredAtUtc = adjustment.AdjustmentDate.ToUniversalTime(),
            OperatorId = operatorItem.Id,
            OperatorSnapshot = operatorItem.DisplayName,
            InitialPieces = adjustment.InitialPieces,
            DiscardedWholeBoards = adjustment.DiscardedWholeBoards,
            GoodPieces = adjustment.GoodPieces,
            AdjustmentBaseCubicMeters = adjustment.AdjustmentBaseCubicMeters,
            CubicMetersBeforeAdjustment = adjustment.CubicMetersBeforeAdjustment,
            CubicMetersAfterWholeBoardWaste = adjustment.CubicMetersAfterWholeBoardWaste,
            PartialWastePercentage = adjustment.PartialWastePercentage,
            PartialWasteCubicMeters = adjustment.PartialWasteCubicMeters,
            RealAvailableCubicMeters = adjustment.RealAvailableCubicMeters,
            WholeBoardWastePercentage = adjustment.WholeBoardWastePercentage,
            TotalClassificationWastePercentage = adjustment.TotalClassificationWastePercentage
        };
        db.WasteAdjustments.Add(entity);
        PersistenceDebugLog.Write($"WasteAdjustment tracking: MaterialGroupEntity={db.Entry(group).State}; " +
            $"WasteAdjustmentEntity={db.Entry(entity).State}.");

        // One SaveChanges is one atomic EF transaction: UPDATE group + INSERT adjustment.
        db.SaveChanges();
        return new PersistedWasteAdjustment(Map(entity), group.RowVersion);
    }

    internal static WasteAdjustment Map(WasteAdjustmentEntity entity) => new()
    {
        AdjustmentId = entity.Id,
        LoadId = entity.LoadId,
        MaterialGroupId = entity.MaterialGroupId,
        AdjustmentDate = entity.OccurredAtUtc.ToLocalTime(),
        AdjustmentOperator = entity.OperatorSnapshot,
        InitialPieces = entity.InitialPieces,
        DiscardedWholeBoards = entity.DiscardedWholeBoards,
        GoodPieces = entity.GoodPieces,
        AdjustmentBaseCubicMeters = entity.AdjustmentBaseCubicMeters,
        CubicMetersBeforeAdjustment = entity.CubicMetersBeforeAdjustment,
        CubicMetersAfterWholeBoardWaste = entity.CubicMetersAfterWholeBoardWaste,
        PartialWastePercentage = entity.PartialWastePercentage,
        PartialWasteCubicMeters = entity.PartialWasteCubicMeters,
        RealAvailableCubicMeters = entity.RealAvailableCubicMeters,
        WholeBoardWastePercentage = entity.WholeBoardWastePercentage,
        TotalClassificationWastePercentage = entity.TotalClassificationWastePercentage
    };
}
