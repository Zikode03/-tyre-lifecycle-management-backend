using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TyreLifecycle.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class RecordWorkshopInspections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TlWorkshopInspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InspectionIdsJson = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    JobCardId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    InspectionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TlWorkshopInspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TlWorkshopInspections_TlVehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "TlVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TlWorkshopInspections_VehicleId",
                table: "TlWorkshopInspections",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TlWorkshopInspections");
        }
    }
}
