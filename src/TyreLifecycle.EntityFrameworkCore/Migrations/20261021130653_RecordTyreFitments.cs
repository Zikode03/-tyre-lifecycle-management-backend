using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TyreLifecycle.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class RecordTyreFitments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM TlTyres GROUP BY UPPER(REPLACE(TyreNumber, ' ', '')) HAVING COUNT(*) > 1)
    THROW 51000, 'Duplicate tyre references must be reviewed before enabling fitments.', 1;
IF EXISTS (SELECT 1 FROM TlTyres WHERE IsActive = 1 AND IsDeleted = 0 GROUP BY VehicleId, UPPER(REPLACE(REPLACE(Position, ' ', ''), '-', '')) HAVING COUNT(*) > 1)
    THROW 51001, 'Occupied wheel position duplicates must be reviewed before enabling fitments.', 1;
");
            migrationBuilder.AddColumn<string>(
                name: "PositionKey",
                table: "TlTyres",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                computedColumnSql: "UPPER(REPLACE(REPLACE([Position], ' ', ''), '-', ''))",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceKey",
                table: "TlTyres",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                computedColumnSql: "UPPER(REPLACE([TyreNumber], ' ', ''))",
                stored: true);

            migrationBuilder.CreateTable(
                name: "TlTyreFitments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TyreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FitmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OdometerKm = table.Column<long>(type: "bigint", nullable: false),
                    InitialTreadMm = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StockSku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TlTyreFitments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TlTyreFitments_TlTyres_TyreId",
                        column: x => x.TyreId,
                        principalTable: "TlTyres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TlTyreFitments_TlVehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "TlVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TlTyres_ReferenceKey",
                table: "TlTyres",
                column: "ReferenceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TlTyres_VehicleId_PositionKey",
                table: "TlTyres",
                columns: new[] { "VehicleId", "PositionKey" },
                unique: true,
                filter: "[IsActive] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TlTyreFitments_TyreId",
                table: "TlTyreFitments",
                column: "TyreId");

            migrationBuilder.CreateIndex(
                name: "IX_TlTyreFitments_VehicleId",
                table: "TlTyreFitments",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TlTyreFitments");

            migrationBuilder.DropIndex(
                name: "IX_TlTyres_ReferenceKey",
                table: "TlTyres");

            migrationBuilder.DropIndex(
                name: "IX_TlTyres_VehicleId_PositionKey",
                table: "TlTyres");

            migrationBuilder.DropColumn(
                name: "PositionKey",
                table: "TlTyres");

            migrationBuilder.DropColumn(
                name: "ReferenceKey",
                table: "TlTyres");
        }
    }
}
