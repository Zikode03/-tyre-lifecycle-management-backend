using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TyreLifecycle.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class RecordTyreRemovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TlTyreRemovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TyreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReplacementTyreId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RemovalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OdometerKm = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TlTyreRemovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TlTyreRemovals_TlTyres_ReplacementTyreId",
                        column: x => x.ReplacementTyreId,
                        principalTable: "TlTyres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TlTyreRemovals_TlTyres_TyreId",
                        column: x => x.TyreId,
                        principalTable: "TlTyres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TlTyreRemovals_TlVehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "TlVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TlTyreRemovals_ReplacementTyreId",
                table: "TlTyreRemovals",
                column: "ReplacementTyreId");

            migrationBuilder.CreateIndex(
                name: "IX_TlTyreRemovals_TyreId",
                table: "TlTyreRemovals",
                column: "TyreId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TlTyreRemovals_VehicleId",
                table: "TlTyreRemovals",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TlTyreRemovals");
        }
    }
}
