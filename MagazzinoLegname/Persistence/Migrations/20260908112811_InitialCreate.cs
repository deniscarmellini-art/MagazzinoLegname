using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MagazzinoLegname.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    DefaultTimberCertification = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StandardCubicMetersPerExpectedLoad23 = table.Column<decimal>(type: "decimal(19,9)", nullable: false),
                    StandardCubicMetersPerExpectedLoad34 = table.Column<decimal>(type: "decimal(19,9)", nullable: false),
                    StandardCubicMetersPerExpectedLoad44 = table.Column<decimal>(type: "decimal(19,9)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Operators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    VatNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThicknessFamilies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinimumIncomingThickness = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    MaximumIncomingThickness = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    ConventionalThickness = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    UsefulProductionThickness = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    StandardWidthReductionMillimeters = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    FingerJointLengthReductionMillimeters = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThicknessFamilies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegacyImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FileFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportedByOperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ImportedBySnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ImportedRows = table.Column<int>(type: "int", nullable: false),
                    LoadCount = table.Column<int>(type: "int", nullable: false),
                    PackageCount = table.Column<int>(type: "int", nullable: false),
                    PhysicalCubicMeters = table.Column<decimal>(type: "decimal(19,9)", nullable: false),
                    LegacyAvailableCubicMeters = table.Column<decimal>(type: "decimal(19,9)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegacyImportBatches_Operators_ImportedByOperatorId",
                        column: x => x.ImportedByOperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoadNumberSequences",
                columns: table => new
                {
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoadYear = table.Column<int>(type: "int", nullable: false),
                    NextProgressive = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoadNumberSequences", x => new { x.SupplierId, x.LoadYear });
                    table.ForeignKey(
                        name: "FK_LoadNumberSequences_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierContacts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConventionalThickness = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    PricePerCubicMeter = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierPrices_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierThicknessConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConventionalThickness = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    IsPlaningEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PlaningReductionMillimeters = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierThicknessConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierThicknessConfigurations_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LegacyHistoricalRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HistoricalLoadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExcelRow = table.Column<int>(type: "int", nullable: false),
                    SupplierNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ArrivalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LoadNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PackageLabel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Pieces = table.Column<decimal>(type: "decimal(19,6)", nullable: false),
                    IncomingThickness = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    IncomingWidth = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    IncomingLength = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    QualityOriginal = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    QualityNormalized = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Certification = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PhysicalCubicMeters = table.Column<decimal>(type: "decimal(19,9)", nullable: false),
                    LegacyAvailableCubicMeters = table.Column<decimal>(type: "decimal(19,9)", nullable: false),
                    LegacyEstimatedCubicMeters = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsClassified = table.Column<bool>(type: "bit", nullable: true),
                    ClassificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinishedRawValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FinishedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LegacyQr = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyHistoricalRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegacyHistoricalRecords_LegacyImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "LegacyImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LegacyImportKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExcelRow = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyImportKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegacyImportKeys_LegacyImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "LegacyImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Loads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoadNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LoadYear = table.Column<int>(type: "int", nullable: true),
                    AnnualProgressive = table.Column<int>(type: "int", nullable: true),
                    Certification = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ArrivalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveryNoteNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ReceiptOperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceiptOperatorSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LegacyLoadNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LegacyImportBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Loads_LegacyImportBatches_LegacyImportBatchId",
                        column: x => x.LegacyImportBatchId,
                        principalTable: "LegacyImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Loads_Operators_ReceiptOperatorId",
                        column: x => x.ReceiptOperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Loads_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaterialGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncomingThickness = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    ConventionalThickness = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    UsefulThickness = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    IncomingWidth = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    WidthAfterPlaning = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    FinalWidth = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    IncomingLength = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    FinalLength = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    Quality = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PackageCount = table.Column<int>(type: "int", nullable: false),
                    InitialPieces = table.Column<int>(type: "int", nullable: false),
                    IncomingPhysicalCubicMeters = table.Column<decimal>(type: "decimal(19,9)", nullable: false),
                    AppliedPrice = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    HistoricalValue = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    IsClassified = table.Column<bool>(type: "bit", nullable: false),
                    WasteVerified = table.Column<bool>(type: "bit", nullable: false),
                    IsLegacyImport = table.Column<bool>(type: "bit", nullable: false),
                    LegacyEstimatedCubicMeters = table.Column<decimal>(type: "decimal(19,9)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialGroups_Loads_LoadId",
                        column: x => x.LoadId,
                        principalTable: "Loads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierReturnOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DocumentReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReturnOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierReturnOperations_Loads_LoadId",
                        column: x => x.LoadId,
                        principalTable: "Loads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturnOperations_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassificationMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassificationMovements_Loads_LoadId",
                        column: x => x.LoadId,
                        principalTable: "Loads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassificationMovements_MaterialGroups_MaterialGroupId",
                        column: x => x.MaterialGroupId,
                        principalTable: "MaterialGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassificationMovements_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Packages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    QrPayload = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PackageType = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    SupplementarySequence = table.Column<int>(type: "int", nullable: true),
                    PieceCount = table.Column<int>(type: "int", nullable: true),
                    IncomingPhysicalCubicMeters = table.Column<decimal>(type: "decimal(19,9)", nullable: false),
                    AppliedPrice = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    HistoricalPackageValue = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    ArrivalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LegacyPackageLabel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    LegacyExcelRow = table.Column<int>(type: "int", nullable: true),
                    LegacyQr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LegacyImportBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packages", x => x.Id);
                    table.CheckConstraint("CK_Packages_SupplementaryNoValue", "[PackageType] = 0 OR ([IncomingPhysicalCubicMeters] = 0 AND [AppliedPrice] IS NULL AND [HistoricalPackageValue] IS NULL)");
                    table.ForeignKey(
                        name: "FK_Packages_Loads_LoadId",
                        column: x => x.LoadId,
                        principalTable: "Loads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Packages_MaterialGroups_MaterialGroupId",
                        column: x => x.MaterialGroupId,
                        principalTable: "MaterialGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WasteAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InitialPieces = table.Column<int>(type: "int", nullable: false),
                    DiscardedWholeBoards = table.Column<int>(type: "int", nullable: false),
                    GoodPieces = table.Column<int>(type: "int", nullable: false),
                    AdjustmentBaseCubicMeters = table.Column<decimal>(type: "decimal(19,9)", nullable: false),
                    TheoreticalUsefulCubicMeters = table.Column<decimal>(type: "decimal(19,9)", nullable: false),
                    CubicMetersAfterWholeBoardWaste = table.Column<decimal>(type: "decimal(19,9)", nullable: false),
                    PartialWastePercentage = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    PartialWasteCubicMeters = table.Column<decimal>(type: "decimal(19,9)", nullable: false),
                    RealAvailableCubicMeters = table.Column<decimal>(type: "decimal(19,9)", nullable: false),
                    WholeBoardWastePercentage = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    TotalClassificationWastePercentage = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WasteAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WasteAdjustments_Loads_LoadId",
                        column: x => x.LoadId,
                        principalTable: "Loads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WasteAdjustments_MaterialGroups_MaterialGroupId",
                        column: x => x.MaterialGroupId,
                        principalTable: "MaterialGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WasteAdjustments_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackageTerminalEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReturnOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InventoryCubicMeters = table.Column<decimal>(type: "decimal(19,9)", nullable: true),
                    ReturnedPhysicalCubicMeters = table.Column<decimal>(type: "decimal(19,9)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageTerminalEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageTerminalEvents_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageTerminalEvents_Packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageTerminalEvents_SupplierReturnOperations_ReturnOperationId",
                        column: x => x.ReturnOperationId,
                        principalTable: "SupplierReturnOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationMovements_LoadId",
                table: "ClassificationMovements",
                column: "LoadId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationMovements_MaterialGroupId",
                table: "ClassificationMovements",
                column: "MaterialGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationMovements_OperatorId",
                table: "ClassificationMovements",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyHistoricalRecords_BatchId",
                table: "LegacyHistoricalRecords",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyHistoricalRecords_ImportKey",
                table: "LegacyHistoricalRecords",
                column: "ImportKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacyImportBatches_ImportedByOperatorId",
                table: "LegacyImportBatches",
                column: "ImportedByOperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyImportBatches_Kind_FileFingerprint",
                table: "LegacyImportBatches",
                columns: new[] { "Kind", "FileFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacyImportKeys_BatchId",
                table: "LegacyImportKeys",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyImportKeys_ImportKey",
                table: "LegacyImportKeys",
                column: "ImportKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loads_LegacyImportBatchId",
                table: "Loads",
                column: "LegacyImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Loads_ReceiptOperatorId",
                table: "Loads",
                column: "ReceiptOperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Loads_SupplierId_LoadYear_AnnualProgressive",
                table: "Loads",
                columns: new[] { "SupplierId", "LoadYear", "AnnualProgressive" },
                unique: true,
                filter: "[LoadYear] IS NOT NULL AND [AnnualProgressive] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialGroups_LoadId",
                table: "MaterialGroups",
                column: "LoadId");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_LoadId",
                table: "Packages",
                column: "LoadId");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_MaterialGroupId",
                table: "Packages",
                column: "MaterialGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_PackageCode",
                table: "Packages",
                column: "PackageCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageTerminalEvents_OperatorId",
                table: "PackageTerminalEvents",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageTerminalEvents_PackageId",
                table: "PackageTerminalEvents",
                column: "PackageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageTerminalEvents_ReturnOperationId",
                table: "PackageTerminalEvents",
                column: "ReturnOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContacts_SupplierId",
                table: "SupplierContacts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPrices_SupplierId_ConventionalThickness_ValidFrom",
                table: "SupplierPrices",
                columns: new[] { "SupplierId", "ConventionalThickness", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnOperations_LoadId",
                table: "SupplierReturnOperations",
                column: "LoadId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnOperations_OperatorId",
                table: "SupplierReturnOperations",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Code",
                table: "Suppliers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierThicknessConfigurations_SupplierId_ConventionalThickness",
                table: "SupplierThicknessConfigurations",
                columns: new[] { "SupplierId", "ConventionalThickness" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThicknessFamilies_ConventionalThickness",
                table: "ThicknessFamilies",
                column: "ConventionalThickness",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WasteAdjustments_LoadId",
                table: "WasteAdjustments",
                column: "LoadId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteAdjustments_MaterialGroupId",
                table: "WasteAdjustments",
                column: "MaterialGroupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WasteAdjustments_OperatorId",
                table: "WasteAdjustments",
                column: "OperatorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationSettings");

            migrationBuilder.DropTable(
                name: "ClassificationMovements");

            migrationBuilder.DropTable(
                name: "LegacyHistoricalRecords");

            migrationBuilder.DropTable(
                name: "LegacyImportKeys");

            migrationBuilder.DropTable(
                name: "LoadNumberSequences");

            migrationBuilder.DropTable(
                name: "PackageTerminalEvents");

            migrationBuilder.DropTable(
                name: "SupplierContacts");

            migrationBuilder.DropTable(
                name: "SupplierPrices");

            migrationBuilder.DropTable(
                name: "SupplierThicknessConfigurations");

            migrationBuilder.DropTable(
                name: "ThicknessFamilies");

            migrationBuilder.DropTable(
                name: "WasteAdjustments");

            migrationBuilder.DropTable(
                name: "Packages");

            migrationBuilder.DropTable(
                name: "SupplierReturnOperations");

            migrationBuilder.DropTable(
                name: "MaterialGroups");

            migrationBuilder.DropTable(
                name: "Loads");

            migrationBuilder.DropTable(
                name: "LegacyImportBatches");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "Operators");
        }
    }
}
