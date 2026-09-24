using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MagazzinoLegname.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsumableOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsumableOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumableItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderedAt = table.Column<DateTime>(type: "date", nullable: false),
                    OrderedQuantity = table.Column<decimal>(type: "decimal(19,6)", nullable: false),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "date", nullable: true),
                    SupplierNameSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ProductNameSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    UnitOfMeasureSnapshot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumableOrders", x => x.Id);
                    table.CheckConstraint("CK_ConsumableOrders_Delivery", "[ExpectedDeliveryDate] IS NULL OR [ExpectedDeliveryDate] >= [OrderedAt]");
                    table.CheckConstraint("CK_ConsumableOrders_Quantity", "[OrderedQuantity] > 0");
                    table.CheckConstraint("CK_ConsumableOrders_Status", "([Status] IN (1,2) AND [ClosedAtUtc] IS NULL) OR ([Status] IN (3,4) AND [ClosedAtUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ConsumableOrders_ConsumableItems_ConsumableItemId",
                        column: x => x.ConsumableItemId,
                        principalTable: "ConsumableItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableOrders_ConsumableItemId_Status",
                table: "ConsumableOrders",
                columns: new[] { "ConsumableItemId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsumableOrders");
        }
    }
}
