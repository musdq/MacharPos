using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MacharPos.Web.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_ShiftClose_Void_CreditControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Sales",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "Sales",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAtUtc",
                table: "Sales",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidedByUserId",
                table: "Sales",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreditDueDays",
                table: "Customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditLimit",
                table: "Customers",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsCreditApproved",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ShiftCloses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftDate = table.Column<DateTime>(type: "date", nullable: false),
                    ShiftName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ShiftStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ShiftEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalSales = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalTransactions = table.Column<int>(type: "integer", nullable: false),
                    CashCollected = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PosCollected = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TransferCollected = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalLpgKg = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreditIssuedTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreditCollectedTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CashCounted = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CashVariance = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    HandedOverTo = table.Column<string>(type: "text", nullable: true),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    ClosedByUserId = table.Column<string>(type: "text", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftCloses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftCloses_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftCloses_StationId_ShiftStartUtc",
                table: "ShiftCloses",
                columns: new[] { "StationId", "ShiftStartUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftCloses");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "VoidedAtUtc",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "VoidedByUserId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CreditDueDays",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreditLimit",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsCreditApproved",
                table: "Customers");
        }
    }
}
