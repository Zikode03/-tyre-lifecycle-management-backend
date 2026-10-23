using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TyreLifecycle.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class ConnectWorkspaceAndAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TlPortalChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Used = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TlPortalChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TlStaffAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RoleLabel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TlStaffAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TlWorkspaceDocuments",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TlWorkspaceDocuments", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TlStaffAccounts_Email",
                table: "TlStaffAccounts",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TlPortalChallenges");

            migrationBuilder.DropTable(
                name: "TlStaffAccounts");

            migrationBuilder.DropTable(
                name: "TlWorkspaceDocuments");
        }
    }
}
