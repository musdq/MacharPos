using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MacharPos.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyClose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyCloses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CloseDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TotalSales = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalTransactions = table.Column<int>(type: "integer", nullable: false),
                    CashTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PosTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TransferTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalLpgKg = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActiveBatchRemainingKg = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CashCounted = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CashVariance = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    ClosedByUserId = table.Column<string>(type: "text", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBySuperAdminId = table.Column<string>(type: "text", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModificationReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCloses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyCloses_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloses_StationId_CloseDate",
                table: "DailyCloses",
                columns: new[] { "StationId", "CloseDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyCloses");
        }
    }
}
