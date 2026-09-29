using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MagazzinoLegname.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlannedArrivals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlannedArrivals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierNameSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ConventionalThickness = table.Column<decimal>(type: "decimal(5,0)", nullable: false),
                    Quality = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    LoadQuantity = table.Column<int>(type: "int", nullable: false),
                    ExpectedCubicMeters = table.Column<decimal>(type: "decimal(28,9)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannedArrivals", x => x.Id);
                    table.CheckConstraint("CK_PlannedArrivals_Material", "[ConventionalThickness] IN (23,34,44) AND [Quality] IN ('C','VISTA')");
                    table.CheckConstraint("CK_PlannedArrivals_Quantity", "[LoadQuantity] > 0 AND [ExpectedCubicMeters] > 0");
                    table.CheckConstraint("CK_PlannedArrivals_Status", "([Status] = 0 AND [ConfirmedAt] IS NULL) OR ([Status] = 1 AND [ConfirmedAt] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PlannedArrivals_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedArrivals_SupplierId_Date",
                table: "PlannedArrivals",
                columns: new[] { "SupplierId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlannedArrivals");
        }
    }
}
