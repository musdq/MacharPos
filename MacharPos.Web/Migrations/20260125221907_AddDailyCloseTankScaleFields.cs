using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MacharPos.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyCloseTankScaleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TankClosingKg",
                table: "DailyCloses",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TankOpeningKg",
                table: "DailyCloses",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TankReceiptsKg",
                table: "DailyCloses",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TankScaleSoldKg",
                table: "DailyCloses",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TankVarianceKg",
                table: "DailyCloses",
                type: "numeric(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TankClosingKg",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "TankOpeningKg",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "TankReceiptsKg",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "TankScaleSoldKg",
                table: "DailyCloses");

            migrationBuilder.DropColumn(
                name: "TankVarianceKg",
                table: "DailyCloses");
        }
    }
}
