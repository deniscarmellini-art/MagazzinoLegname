using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MagazzinoLegname.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyTimberDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FingerJointLengthReductionMillimeters",
                table: "ThicknessFamilies");

            migrationBuilder.DropColumn(
                name: "StandardWidthReductionMillimeters",
                table: "ThicknessFamilies");

            migrationBuilder.DropColumn(
                name: "UsefulProductionThickness",
                table: "ThicknessFamilies");

            migrationBuilder.DropColumn(
                name: "FinalLength",
                table: "MaterialGroups");

            migrationBuilder.DropColumn(
                name: "FinalWidth",
                table: "MaterialGroups");

            migrationBuilder.DropColumn(
                name: "UsefulThickness",
                table: "MaterialGroups");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FingerJointLengthReductionMillimeters",
                table: "ThicknessFamilies",
                type: "decimal(12,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "StandardWidthReductionMillimeters",
                table: "ThicknessFamilies",
                type: "decimal(12,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UsefulProductionThickness",
                table: "ThicknessFamilies",
                type: "decimal(12,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalLength",
                table: "MaterialGroups",
                type: "decimal(12,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalWidth",
                table: "MaterialGroups",
                type: "decimal(12,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UsefulThickness",
                table: "MaterialGroups",
                type: "decimal(12,4)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
