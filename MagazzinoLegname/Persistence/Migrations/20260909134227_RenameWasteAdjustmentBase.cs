using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MagazzinoLegname.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameWasteAdjustmentBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TheoreticalUsefulCubicMeters",
                table: "WasteAdjustments",
                newName: "CubicMetersBeforeAdjustment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CubicMetersBeforeAdjustment",
                table: "WasteAdjustments",
                newName: "TheoreticalUsefulCubicMeters");
        }
    }
}
