using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TyreLifecycle.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class RecordApprovedJobWork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TlCompletedWorkshopJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobCardId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TlCompletedWorkshopJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TlCompletedWorkshopJobs_TlVehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "TlVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TlJobWorkRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobCardId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ItemId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ItemJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FitmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TyresJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PerformedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OdometerKm = table.Column<long>(type: "bigint", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TlJobWorkRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TlJobWorkRecords_TlTyreFitments_FitmentId",
                        column: x => x.FitmentId,
                        principalTable: "TlTyreFitments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TlJobWorkRecords_TlTyreRotations_RotationId",
                        column: x => x.RotationId,
                        principalTable: "TlTyreRotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TlJobWorkRecords_TlVehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "TlVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TlCompletedWorkshopJobs_JobCardId",
                table: "TlCompletedWorkshopJobs",
                column: "JobCardId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TlCompletedWorkshopJobs_VehicleId",
                table: "TlCompletedWorkshopJobs",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_TlJobWorkRecords_FitmentId",
                table: "TlJobWorkRecords",
                column: "FitmentId",
                unique: true,
                filter: "[FitmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TlJobWorkRecords_JobCardId_ItemId",
                table: "TlJobWorkRecords",
                columns: new[] { "JobCardId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TlJobWorkRecords_RotationId",
                table: "TlJobWorkRecords",
                column: "RotationId",
                unique: true,
                filter: "[RotationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TlJobWorkRecords_VehicleId",
                table: "TlJobWorkRecords",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TlCompletedWorkshopJobs");

            migrationBuilder.DropTable(
                name: "TlJobWorkRecords");
        }
    }
}
