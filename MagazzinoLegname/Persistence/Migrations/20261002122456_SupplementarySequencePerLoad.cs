using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MagazzinoLegname.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplementarySequencePerLoad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM dbo.Packages
                    WHERE PackageType = 1 AND SupplementarySequence IS NOT NULL
                    GROUP BY LoadId, SupplementarySequence HAVING COUNT(*) > 1)
                    THROW 51002, 'Progressivi supplementari duplicati per carico: verifica manuale richiesta; nessun dato rinumerato.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Packages_MaterialGroupId_PackageType_SupplementarySequence",
                table: "Packages");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_LoadId_PackageType_SupplementarySequence",
                table: "Packages",
                columns: new[] { "LoadId", "PackageType", "SupplementarySequence" },
                unique: true,
                filter: "[PackageType] = 1 AND [SupplementarySequence] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_MaterialGroupId",
                table: "Packages",
                column: "MaterialGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Packages_LoadId_PackageType_SupplementarySequence",
                table: "Packages");

            migrationBuilder.DropIndex(
                name: "IX_Packages_MaterialGroupId",
                table: "Packages");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_MaterialGroupId_PackageType_SupplementarySequence",
                table: "Packages",
                columns: new[] { "MaterialGroupId", "PackageType", "SupplementarySequence" },
                unique: true,
                filter: "[PackageType] = 1 AND [SupplementarySequence] IS NOT NULL");
        }
    }
}
