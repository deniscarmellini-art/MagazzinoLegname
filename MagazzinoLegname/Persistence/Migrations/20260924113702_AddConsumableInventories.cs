using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MagazzinoLegname.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsumableInventories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsumableInventorySessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryDate = table.Column<DateTime>(type: "date", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorSnapshot = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumableInventorySessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumableInventorySessions_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsumableInventoryReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumableItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountedUnits = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    QuantityPerUnitSnapshot = table.Column<decimal>(type: "decimal(19,6)", nullable: false),
                    CalculatedQuantity = table.Column<decimal>(type: "decimal(28,12)", nullable: false),
                    ProductSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SupplierSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DepartmentSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnitOfMeasureSnapshot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumableInventoryReadings", x => x.Id);
                    table.CheckConstraint("CK_ConsumableInventoryReadings_Calculation", "[CalculatedQuantity] = [CountedUnits] * [QuantityPerUnitSnapshot]");
                    table.CheckConstraint("CK_ConsumableInventoryReadings_Quantities", "[CountedUnits] >= 0 AND [QuantityPerUnitSnapshot] > 0 AND [CalculatedQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_ConsumableInventoryReadings_ConsumableInventorySessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ConsumableInventorySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsumableInventoryReadings_ConsumableItems_ConsumableItemId",
                        column: x => x.ConsumableItemId,
                        principalTable: "ConsumableItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableInventoryReadings_ConsumableItemId",
                table: "ConsumableInventoryReadings",
                column: "ConsumableItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableInventoryReadings_SessionId_ConsumableItemId",
                table: "ConsumableInventoryReadings",
                columns: new[] { "SessionId", "ConsumableItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableInventorySessions_InventoryDate_CreatedAtUtc",
                table: "ConsumableInventorySessions",
                columns: new[] { "InventoryDate", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableInventorySessions_OperatorId",
                table: "ConsumableInventorySessions",
                column: "OperatorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsumableInventoryReadings");

            migrationBuilder.DropTable(
                name: "ConsumableInventorySessions");
        }
    }
}
