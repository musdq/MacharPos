using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MacharPos.Web.Migrations
{
    /// <inheritdoc />
    public partial class DailyCloseV2_CreditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CreditCollectedCash",
                table: "DailyCloses",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditCollectedPos",
                table: "DailyCloses",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditCollectedTotal",
                table: "DailyCloses",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditCollectedTransfer",
                table: "DailyCloses",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditIssuedTotal",
                table: "DailyCloses",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreditCollectedCash",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "CreditCollectedPos",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "CreditCollectedTotal",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "CreditCollectedTransfer",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "CreditIssuedTotal",
                table: "DailyCloses");
        }
    }
}
