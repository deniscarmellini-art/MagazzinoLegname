using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MagazzinoLegname.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierCompleteProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CertifiedEmail",
                table: "Suppliers",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxCode",
                table: "Suppliers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CertifiedEmail",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TaxCode",
                table: "Suppliers");
        }
    }
}
