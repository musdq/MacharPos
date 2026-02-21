using MacharPos.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MacharPos.Web.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260221120000_Phase6_SaleItemCostStamp")]
    public partial class Phase6_SaleItemCostStamp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitCostAtSale",
                table: "SaleItems",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCostAtSale",
                table: "SaleItems",
                type: "numeric",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitCostAtSale",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "TotalCostAtSale",
                table: "SaleItems");
        }
    }
}
