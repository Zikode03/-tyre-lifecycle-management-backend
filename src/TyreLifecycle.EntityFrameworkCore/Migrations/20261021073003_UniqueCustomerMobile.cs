using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TyreLifecycle.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class UniqueCustomerMobile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                WITH normalized AS (
                    SELECT Id, REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(MobileNumber,' ',''),'-',''),'(',''),')',''),'+',''),CHAR(9),'') AS Digits
                    FROM TlCustomers WHERE IsDeleted=0
                )
                UPDATE c SET MobileNumber=CASE
                    WHEN LEFT(n.Digits,4)='0027' THEN '0'+SUBSTRING(n.Digits,5,32)
                    WHEN LEFT(n.Digits,2)='27' THEN '0'+SUBSTRING(n.Digits,3,32)
                    ELSE n.Digits END
                FROM TlCustomers c INNER JOIN normalized n ON n.Id=c.Id;
                IF EXISTS(SELECT 1 FROM TlCustomers WHERE IsDeleted=0 AND
                    (LEN(MobileNumber)<>10 OR LEFT(MobileNumber,2) NOT IN ('06','07','08') OR MobileNumber LIKE '%[^0-9]%'))
                    THROW 50000, 'Correct invalid customer mobile numbers before applying this migration.', 1;
            ");
            migrationBuilder.CreateIndex(
                name: "IX_TlCustomers_MobileNumber",
                table: "TlCustomers",
                column: "MobileNumber",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TlCustomers_MobileNumber",
                table: "TlCustomers");
        }
    }
}
