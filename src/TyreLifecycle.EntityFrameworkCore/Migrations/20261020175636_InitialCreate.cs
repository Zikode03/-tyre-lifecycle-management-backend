using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TyreLifecycle.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TlCustomers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MobileNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TlCustomers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TlInspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnicianUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OdometerKm = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TlInspections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TlTyres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TyreNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Size = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TreadDepthMm = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    HealthStatus = table.Column<int>(type: "int", nullable: false),
                    FitmentOdometerKm = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TlTyres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TlVehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Make = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    OdometerKm = table.Column<long>(type: "bigint", nullable: false),
                    Vin = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TlVehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TlInspectionTyreReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TyreId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Position = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    InnerTreadMm = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    CentreTreadMm = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    OuterTreadMm = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    PressurePsi = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: true),
                    WearPattern = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Defects = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Recommendation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TlInspectionTyreReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TlInspectionTyreReadings_TlInspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "TlInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TlCustomers_CustomerNumber",
                table: "TlCustomers",
                column: "CustomerNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TlInspections_InspectionNumber",
                table: "TlInspections",
                column: "InspectionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TlInspections_Status",
                table: "TlInspections",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TlInspections_VehicleId",
                table: "TlInspections",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_TlInspectionTyreReadings_InspectionId",
                table: "TlInspectionTyreReadings",
                column: "InspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_TlInspectionTyreReadings_InspectionId_Position",
                table: "TlInspectionTyreReadings",
                columns: new[] { "InspectionId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TlInspectionTyreReadings_TyreId",
                table: "TlInspectionTyreReadings",
                column: "TyreId");

            migrationBuilder.CreateIndex(
                name: "IX_TlTyres_TyreNumber",
                table: "TlTyres",
                column: "TyreNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TlTyres_VehicleId",
                table: "TlTyres",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_TlVehicles_CustomerId",
                table: "TlVehicles",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_TlVehicles_RegistrationNumber",
                table: "TlVehicles",
                column: "RegistrationNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TlCustomers");

            migrationBuilder.DropTable(
                name: "TlInspectionTyreReadings");

            migrationBuilder.DropTable(
                name: "TlTyres");

            migrationBuilder.DropTable(
                name: "TlVehicles");

            migrationBuilder.DropTable(
                name: "TlInspections");
        }
    }
}
