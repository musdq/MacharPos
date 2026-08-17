using MacharPos.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MacharPos.Web.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260807170000_AddExpenseManagement")]
public partial class AddExpenseManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ExpenseRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                StationId = table.Column<Guid>(type: "uuid", nullable: false),
                RequestedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                ExpenseDateTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                Category = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                EvidenceOriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                EvidenceContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                EvidenceFileSize = table.Column<long>(type: "bigint", nullable: true),
                EvidenceData = table.Column<byte[]>(type: "bytea", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReviewedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ReviewNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExpenseRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_ExpenseRequests_Stations_StationId",
                    column: x => x.StationId,
                    principalTable: "Stations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExpenseRequests_RequestedByUserId_CreatedAtUtc",
            table: "ExpenseRequests",
            columns: new[] { "RequestedByUserId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ExpenseRequests_StationId_ExpenseDateTimeUtc",
            table: "ExpenseRequests",
            columns: new[] { "StationId", "ExpenseDateTimeUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ExpenseRequests_Status_CreatedAtUtc",
            table: "ExpenseRequests",
            columns: new[] { "Status", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ExpenseRequests");
    }
}
