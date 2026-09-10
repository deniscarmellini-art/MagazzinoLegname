using System.Data;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence.Entities;
using MagazzinoLegname.Services;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Persistence.Repositories;

public sealed record PersistedInboundLoad(ClassificationLoad Load, IReadOnlyList<PhysicalPackageDraft> Packages,
    IReadOnlyList<SupplementaryPackage> SupplementaryPackages, IReadOnlyList<WasteAdjustment> WasteAdjustments);

public interface IInboundLoadRepository
{
    LoadNumberAssignment PreviewNext(Guid supplierId, int year);
    PersistedInboundLoad Register(GoodsReceiptLoadDraft draft, Supplier supplier, Operator receiptOperator,
        DateTime arrivalDate, int expectedPackages, IReadOnlyList<GoodsReceiptLine> lines);
    IReadOnlyList<PersistedInboundLoad> GetAll();
    SupplementaryPackage AddSupplementary(ClassificationLoad load, MaterialGroupClassification group,
        string operatorName, DateTime createdAt);
}

public sealed class SqlInboundLoadRepository(IDbContextFactory<MagazzinoDbContext> contextFactory) : IInboundLoadRepository
{
    public LoadNumberAssignment PreviewNext(Guid supplierId, int year)
    {
        using var db = contextFactory.CreateDbContext();
        var sequenceNext = db.LoadNumberSequences.AsNoTracking()
            .Where(x => x.SupplierId == supplierId && x.LoadYear == year)
            .Select(x => (int?)x.NextProgressive).SingleOrDefault();
        var loadNext = db.Loads.AsNoTracking()
            .Where(x => x.SupplierId == supplierId && x.LoadYear == year && x.AnnualProgressive != null)
            .Max(x => (int?)x.AnnualProgressive) + 1;
        return new LoadNumberAssignment(supplierId, year, Math.Max(sequenceNext ?? 1, loadNext ?? 1));
    }

    public PersistedInboundLoad Register(GoodsReceiptLoadDraft draft, Supplier supplier, Operator receiptOperator,
        DateTime arrivalDate, int expectedPackages, IReadOnlyList<GoodsReceiptLine> lines)
    {
        using var strategyContext = contextFactory.CreateDbContext();
        var strategy = strategyContext.Database.CreateExecutionStrategy();
        PersistedInboundLoad? result = null;
        strategy.Execute(() =>
        {
            using var db = contextFactory.CreateDbContext();
            using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
            var supplierEntity = db.Suppliers.SingleOrDefault(x => x.Id == supplier.Id && x.IsActive)
                ?? throw new InvalidOperationException("Il fornitore non è più disponibile nel database.");
            var operatorEntity = db.Operators.SingleOrDefault(x => x.Id == receiptOperator.Id && x.IsActive)
                ?? throw new InvalidOperationException("L'operatore non è più disponibile nel database.");

            var year = arrivalDate.Year;
            var sequence = db.LoadNumberSequences
                .FromSqlInterpolated($"SELECT * FROM dbo.LoadNumberSequences WITH (UPDLOCK, HOLDLOCK) WHERE SupplierId = {supplier.Id} AND LoadYear = {year}")
                .SingleOrDefault();
            var occupiedMaximum = db.Loads
                .Where(x => x.SupplierId == supplier.Id && x.LoadYear == year && x.AnnualProgressive != null)
                .Max(x => (int?)x.AnnualProgressive) ?? 0;
            var progressive = Math.Max(sequence?.NextProgressive ?? 1, occupiedMaximum + 1);
            if (sequence is null)
            {
                sequence = new LoadNumberSequenceEntity { SupplierId = supplier.Id, LoadYear = year, NextProgressive = progressive + 1 };
                db.LoadNumberSequences.Add(sequence);
            }
            else sequence.NextProgressive = progressive + 1;

            var assignment = new LoadNumberAssignment(supplier.Id, year, progressive);
            var numberedDraft = new GoodsReceiptLoadDraft { Id = draft.Id, DeliveryNoteNumber = draft.DeliveryNoteNumber };
            numberedDraft.AssignNumber(assignment, supplier.Code);
            numberedDraft.CaptureCertification(draft.CertificationApplied);
            var packages = new PackageExpansionService().Expand(numberedDraft, arrivalDate, lines);

            var loadEntity = new LoadEntity
            {
                Id = draft.Id, SupplierId = supplier.Id, Supplier = supplierEntity, LoadNumber = assignment.LoadNumber,
                LoadYear = year, AnnualProgressive = progressive, ArrivalDate = arrivalDate.Date,
                DeliveryNoteNumber = NullIfEmpty(draft.DeliveryNoteNumber), Certification = draft.CertificationApplied,
                ReceiptOperatorId = receiptOperator.Id, ReceiptOperator = operatorEntity, ReceiptOperatorSnapshot = receiptOperator.DisplayName,
                ExpectedPackages = expectedPackages
            };
            foreach (var line in lines)
                loadEntity.MaterialGroups.Add(new MaterialGroupEntity
                {
                    Id = line.GroupId, LoadId = draft.Id, IncomingThickness = line.IncomingThickness,
                    ConventionalThickness = line.ConventionalThickness,
                    IncomingWidth = line.IncomingWidth, WidthAfterPlaning = line.WidthAfterPlaning,
                    IncomingLength = line.IncomingLength,
                    Quality = line.Quality, PackageCount = line.PackageCount, InitialPieces = line.EnteredPieces,
                    IncomingPhysicalCubicMeters = line.PhysicalIncomingCubicMeters,
                    AppliedPrice = line.PrezzoApplicato, HistoricalValue = line.LineValue,
                    IsClassified = false, WasteVerified = false, IsLegacyImport = false
                });
            var pricesByGroup = lines.ToDictionary(x => x.GroupId, x => (Price: (decimal?)x.PrezzoApplicato, Value: (decimal?)x.LineValue));
            foreach (var package in packages)
            {
                var groupPrice = pricesByGroup[package.OriginGroupId];
                loadEntity.Packages.Add(new PackageEntity
                {
                    Id = package.Id, LoadId = draft.Id, MaterialGroupId = package.OriginGroupId,
                    PackageCode = package.PackageCode, QrPayload = package.QrPayload,
                    PackageType = PersistentPackageType.Official, SequenceNumber = package.SequenceNumber,
                    PieceCount = package.PieceCount, TotalOfficialPackages = package.TotalPackages,
                    Status = package.Status, IncomingPhysicalCubicMeters = package.IncomingPhysicalCubicMeters,
                    AppliedPrice = groupPrice.Price,
                    HistoricalPackageValue = groupPrice.Price.HasValue ? package.IncomingPhysicalCubicMeters * groupPrice.Price.Value : null,
                    ArrivalDate = package.ArrivalDate
                });
            }
            db.Loads.Add(loadEntity);
            db.SaveChanges();
            transaction.Commit();
            db.ChangeTracker.Clear();
            var reloaded = db.Loads.AsNoTracking().Include(x => x.Supplier).Include(x => x.MaterialGroups).ThenInclude(x => x.ClassificationMovements)
                .Include(x => x.MaterialGroups).ThenInclude(x => x.WasteAdjustments)
                .Include(x => x.Packages).Single(x => x.Id == draft.Id);
            result = Map(reloaded);
        });
        if (result is null) throw new InvalidOperationException("La registrazione SQL del carico non è stata completata.");
        draft.AssignNumber(new LoadNumberAssignment(supplier.Id, result.Load.LoadYear!.Value, result.Load.AnnualProgressive!.Value), supplier.Code);
        return result;
    }

    public IReadOnlyList<PersistedInboundLoad> GetAll()
    {
        using var db = contextFactory.CreateDbContext();
        return db.Loads.AsNoTracking().Include(x => x.Supplier).Include(x => x.MaterialGroups).ThenInclude(x => x.ClassificationMovements)
            .Include(x => x.MaterialGroups).ThenInclude(x => x.WasteAdjustments)
            .Include(x => x.Packages).OrderBy(x => x.ArrivalDate).ThenBy(x => x.AnnualProgressive)
            .AsEnumerable().Select(Map).ToList();
    }

    public SupplementaryPackage AddSupplementary(ClassificationLoad load, MaterialGroupClassification group,
        string operatorName, DateTime createdAt)
    {
        using var strategyContext = contextFactory.CreateDbContext();
        var strategy = strategyContext.Database.CreateExecutionStrategy();
        SupplementaryPackage? result = null;
        strategy.Execute(() =>
        {
            using var db = contextFactory.CreateDbContext();
            using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
            if (!db.MaterialGroups.Any(x => x.Id == group.GroupId && x.LoadId == load.Id))
                throw new InvalidOperationException("Il gruppo materiale non è presente nel database.");
            var existingSupplementaries = db.Packages
                .FromSqlInterpolated($"SELECT * FROM dbo.Packages WITH (UPDLOCK, HOLDLOCK) WHERE MaterialGroupId = {group.GroupId} AND PackageType = {(int)PersistentPackageType.Supplementary}")
                .ToList();
            var next = (existingSupplementaries.Max(x => x.SupplementarySequence) ?? 0) + 1;
            var code = $"{load.SupplierCode}-{load.AnnualProgressive ?? 0}-{(load.LoadYear ?? load.ArrivalDate.Year) % 100:00}-S{next:00}";
            var qr = QrCodeService.BuildSupplementaryPayload(code, group, load.ArrivalDate);
            var entity = new PackageEntity { Id = Guid.NewGuid(), LoadId = load.Id, MaterialGroupId = group.GroupId,
                PackageCode = code, QrPayload = qr, PackageType = PersistentPackageType.Supplementary,
                SequenceNumber = 0, SupplementarySequence = next, PieceCount = null, TotalOfficialPackages = 0,
                Status = "Presente", IncomingPhysicalCubicMeters = 0m, AppliedPrice = null,
                HistoricalPackageValue = null, ArrivalDate = load.ArrivalDate };
            db.Packages.Add(entity); db.SaveChanges(); transaction.Commit();
            result = new SupplementaryPackage { Id = entity.Id, LoadId = load.Id, MaterialGroupId = group.GroupId,
                PackageCode = code, QrPayload = qr, SupplementarySequence = next, SupplierName = load.SupplierName,
                SupplierCode = load.SupplierCode, LoadNumber = load.LoadNumber, ArrivalDate = load.ArrivalDate,
                IncomingThickness = group.IncomingThickness, ConventionalThickness = group.ConventionalThickness,
                IncomingWidth = group.IncomingWidth, WidthAfterPlaning = group.WidthAfterPlaning,
                IncomingLength = group.IncomingLength, Quality = group.Quality, Certification = load.Certification,
                CreatedAt = createdAt, CreatedBy = operatorName };
        });
        return result ?? throw new InvalidOperationException("Il pacco supplementare non è stato registrato.");
    }

    private static PersistedInboundLoad Map(LoadEntity entity)
    {
        var groups = entity.MaterialGroups.OrderBy(x => x.Id).Select(x =>
        {
            var group = new MaterialGroupClassification
            {
                GroupId = x.Id, LoadId = x.LoadId, IncomingThickness = x.IncomingThickness,
                ConventionalThickness = x.ConventionalThickness,
                IncomingWidth = x.IncomingWidth, WidthAfterPlaning = x.WidthAfterPlaning,
                IncomingLength = x.IncomingLength, Quality = x.Quality,
                PackageCount = x.PackageCount, InitialPieces = x.InitialPieces, AppliedPrice = x.AppliedPrice,
                LineValue = x.HistoricalValue, IsLegacyImport = x.IsLegacyImport, RowVersion = x.RowVersion
            };
            var movement = x.ClassificationMovements.OrderByDescending(m => m.OccurredAtUtc).FirstOrDefault();
            group.ApplyPersistedClassification(x.RowVersion, x.IsClassified,
                movement?.OccurredAtUtc.ToLocalTime(), movement?.OperatorSnapshot,
                x.OfficialLabelsPrintedAt, x.OfficialLabelsPrintedBy);
            if (x.WasteVerified) group.MarkWasteAsVerified();
            return group;
        }).ToList();
        var load = new ClassificationLoad(groups) { Id = entity.Id, SupplierId = entity.SupplierId,
            LoadNumber = entity.LoadNumber, LoadYear = entity.LoadYear, AnnualProgressive = entity.AnnualProgressive,
            SupplierName = entity.Supplier.Name, SupplierCode = entity.Supplier.Code, Certification = entity.Certification,
            ArrivalDate = entity.ArrivalDate, DeliveryNoteNumber = entity.DeliveryNoteNumber ?? string.Empty,
            ReceiptOperator = entity.ReceiptOperatorSnapshot ?? string.Empty, RowVersion = entity.RowVersion };
        var packages = entity.Packages.Where(x => x.PackageType == PersistentPackageType.Official).OrderBy(x => x.SequenceNumber)
            .Select(x => new PhysicalPackageDraft(x.Id, x.LoadId, x.MaterialGroupId, x.SequenceNumber, x.PieceCount ?? 0,
                groups.Single(g => g.GroupId == x.MaterialGroupId).IncomingThickness,
                groups.Single(g => g.GroupId == x.MaterialGroupId).IncomingWidth,
                groups.Single(g => g.GroupId == x.MaterialGroupId).WidthAfterPlaning,
                groups.Single(g => g.GroupId == x.MaterialGroupId).IncomingLength,
                groups.Single(g => g.GroupId == x.MaterialGroupId).Quality)
                { TotalPackages = x.TotalOfficialPackages, ArrivalDate = x.ArrivalDate, Status = x.Status,
                    PackageCode = x.PackageCode, QrPayload = x.QrPayload, AppliedPrice = x.AppliedPrice,
                    RowVersion = x.RowVersion }).ToList();
        var supplementaryPackages = entity.Packages.Where(x => x.PackageType == PersistentPackageType.Supplementary)
            .OrderBy(x => x.SupplementarySequence).Select(x =>
            {
                var group = groups.Single(g => g.GroupId == x.MaterialGroupId);
                return new SupplementaryPackage { Id = x.Id, LoadId = x.LoadId, MaterialGroupId = x.MaterialGroupId,
                    PackageCode = x.PackageCode, QrPayload = x.QrPayload, SupplementarySequence = x.SupplementarySequence ?? 0,
                    SupplierName = load.SupplierName, SupplierCode = load.SupplierCode, LoadNumber = load.LoadNumber,
                    ArrivalDate = load.ArrivalDate, IncomingThickness = group.IncomingThickness,
                    ConventionalThickness = group.ConventionalThickness, IncomingWidth = group.IncomingWidth,
                    WidthAfterPlaning = group.WidthAfterPlaning, IncomingLength = group.IncomingLength,
                    Quality = group.Quality, Certification = load.Certification, CreatedAt = x.ArrivalDate,
                    CreatedBy = load.ReceiptOperator };
            }).ToList();
        var adjustments = entity.MaterialGroups.SelectMany(x => x.WasteAdjustments)
            .OrderBy(x => x.OccurredAtUtc).Select(SqlWasteAdjustmentRepository.Map).ToList();
        return new PersistedInboundLoad(load, packages, supplementaryPackages, adjustments);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
