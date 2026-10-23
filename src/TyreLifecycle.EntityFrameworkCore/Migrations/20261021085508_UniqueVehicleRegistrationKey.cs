using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TyreLifecycle.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class UniqueVehicleRegistrationKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT UPPER(REPLACE(REPLACE(RegistrationNumber,' ',''),'-',''))
                    FROM TlVehicles GROUP BY UPPER(REPLACE(REPLACE(RegistrationNumber,' ',''),'-','')) HAVING COUNT(*)>1)
                    THROW 50000, 'Resolve duplicate vehicle registrations before applying this migration. No vehicle records were changed.', 1;
            ");
            migrationBuilder.AddColumn<string>(
                name: "RegistrationKey",
                table: "TlVehicles",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                computedColumnSql: "UPPER(REPLACE(REPLACE([RegistrationNumber], ' ', ''), '-', ''))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_TlVehicles_RegistrationKey",
                table: "TlVehicles",
                column: "RegistrationKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TlVehicles_RegistrationKey",
                table: "TlVehicles");

            migrationBuilder.DropColumn(
                name: "RegistrationKey",
                table: "TlVehicles");
        }
    }
}
