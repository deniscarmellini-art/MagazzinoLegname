using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MagazzinoLegname.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlannedConsumptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlannedConsumptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    ConventionalThickness = table.Column<decimal>(type: "decimal(5,0)", nullable: false),
                    Quality = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ExpectedCubicMeters = table.Column<decimal>(type: "decimal(28,9)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannedConsumptions", x => x.Id);
                    table.CheckConstraint("CK_PlannedConsumptions_Material", "[ConventionalThickness] IN (23,34,44) AND [Quality] IN ('C','VISTA')");
                    table.CheckConstraint("CK_PlannedConsumptions_Quantity", "[ExpectedCubicMeters] > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedConsumptions_Date_ConventionalThickness_Quality",
                table: "PlannedConsumptions",
                columns: new[] { "Date", "ConventionalThickness", "Quality" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlannedConsumptions");
        }
    }
}
