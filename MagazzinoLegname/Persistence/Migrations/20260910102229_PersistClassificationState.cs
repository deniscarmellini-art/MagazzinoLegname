using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MagazzinoLegname.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistClassificationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Packages_MaterialGroupId",
                table: "Packages");

            migrationBuilder.AddColumn<DateTime>(
                name: "OfficialLabelsPrintedAt",
                table: "MaterialGroups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficialLabelsPrintedBy",
                table: "MaterialGroups",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packages_MaterialGroupId_PackageType_SupplementarySequence",
                table: "Packages",
                columns: new[] { "MaterialGroupId", "PackageType", "SupplementarySequence" },
                unique: true,
                filter: "[PackageType] = 1 AND [SupplementarySequence] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Packages_MaterialGroupId_PackageType_SupplementarySequence",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "OfficialLabelsPrintedAt",
                table: "MaterialGroups");

            migrationBuilder.DropColumn(
                name: "OfficialLabelsPrintedBy",
                table: "MaterialGroups");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_MaterialGroupId",
                table: "Packages",
                column: "MaterialGroupId");
        }
    }
}
