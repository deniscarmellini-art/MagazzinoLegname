using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MagazzinoLegname.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestoreWeeklyPlannedConsumptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Date",
                table: "PlannedConsumptions",
                newName: "WeekStart");

            migrationBuilder.RenameIndex(
                name: "IX_PlannedConsumptions_Date_ConventionalThickness_Quality",
                table: "PlannedConsumptions",
                newName: "IX_PlannedConsumptions_WeekStart_ConventionalThickness_Quality");

            // Preserve weekly totals when the erroneous daily version already contains data.
            // Monday calculation is independent of SET DATEFIRST and session language.
            migrationBuilder.Sql("""
                WITH Normalized AS (
                    SELECT *, DATEADD(day, -((DATEDIFF(day, '19000101', [WeekStart]) % 7 + 7) % 7), [WeekStart]) AS Monday
                    FROM [PlannedConsumptions]
                )
                SELECT [Id], Monday,
                    SUM([ExpectedCubicMeters]) OVER (PARTITION BY Monday, [ConventionalThickness], [Quality]) AS Total,
                    ROW_NUMBER() OVER (PARTITION BY Monday, [ConventionalThickness], [Quality] ORDER BY [CreatedAtUtc], [Id]) AS Position
                INTO #WeeklyConsumptions
                FROM Normalized;

                IF EXISTS (SELECT 1 FROM #WeeklyConsumptions WHERE Total >= 10000000000000000000)
                    THROW 51000, 'Totale settimanale fuori precisione: conversione annullata, dati originali conservati.', 1;

                DELETE target FROM [PlannedConsumptions] target
                INNER JOIN #WeeklyConsumptions weekly ON target.Id = weekly.Id
                WHERE weekly.Position > 1;

                UPDATE target SET [WeekStart] = weekly.Monday, [ExpectedCubicMeters] = weekly.Total,
                    [UpdatedAtUtc] = SYSUTCDATETIME()
                FROM [PlannedConsumptions] target
                INNER JOIN #WeeklyConsumptions weekly ON target.Id = weekly.Id
                WHERE weekly.Position = 1
                    AND (target.[WeekStart] <> weekly.Monday OR target.[ExpectedCubicMeters] <> weekly.Total);

                DROP TABLE #WeeklyConsumptions;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlannedConsumptions_WeekStart",
                table: "PlannedConsumptions",
                sql: "DATEDIFF(day, '19000101', [WeekStart]) % 7 = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Schema rollback retains weekly totals on Mondays; it cannot reconstruct daily allocation.
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlannedConsumptions_WeekStart",
                table: "PlannedConsumptions");

            migrationBuilder.RenameColumn(
                name: "WeekStart",
                table: "PlannedConsumptions",
                newName: "Date");

            migrationBuilder.RenameIndex(
                name: "IX_PlannedConsumptions_WeekStart_ConventionalThickness_Quality",
                table: "PlannedConsumptions",
                newName: "IX_PlannedConsumptions_Date_ConventionalThickness_Quality");
        }
    }
}
