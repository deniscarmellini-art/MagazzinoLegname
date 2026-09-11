using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MagazzinoLegname.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsumableCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsumableItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternalCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    QuantityPerUnit = table.Column<decimal>(type: "decimal(19,6)", nullable: true),
                    MinimumStock = table.Column<decimal>(type: "decimal(19,6)", nullable: true),
                    ConsumptionAverageText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConsumptionAverageQuantity = table.Column<decimal>(type: "decimal(19,6)", nullable: true),
                    ConsumptionPeriod = table.Column<int>(type: "int", nullable: false),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: true),
                    LeadTimeText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Packaging = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumableItems", x => x.Id);
                    table.CheckConstraint("CK_ConsumableItems_BaseParameters", "([MinimumStock] IS NULL OR [MinimumStock] >= 0) AND ([ConsumptionAverageQuantity] IS NULL OR [ConsumptionAverageQuantity] >= 0) AND ([LeadTimeDays] IS NULL OR [LeadTimeDays] >= 0)");
                    table.CheckConstraint("CK_ConsumableItems_QuantityPerUnit", "[QuantityPerUnit] IS NULL OR [QuantityPerUnit] > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableItems_InternalCode",
                table: "ConsumableItems",
                column: "InternalCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsumableItems");
        }
    }
}
