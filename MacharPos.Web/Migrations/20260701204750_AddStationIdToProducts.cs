using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MacharPos.Web.Migrations
{
    public partial class AddStationIdToProducts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add StationId as nullable first
            migrationBuilder.AddColumn<Guid>(
                name: "StationId",
                table: "Products",
                type: "uuid",
                nullable: true);

            // 2. Assign all existing products to one real station
            // Change the WHERE/ORDER BY if you specifically want Borno or Masaka
            migrationBuilder.Sql("""
                UPDATE "Products"
                SET "StationId" = (
                    SELECT "Id"
                    FROM "Stations"       
                    LIMIT 1
                )
                WHERE "StationId" IS NULL;
            """);

            // 3. Make StationId required
            migrationBuilder.AlterColumn<Guid>(
                name: "StationId",
                table: "Products",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // 4. Create index
            migrationBuilder.CreateIndex(
                name: "IX_Products_StationId",
                table: "Products",
                column: "StationId");

            // 5. Add foreign key
            migrationBuilder.AddForeignKey(
                name: "FK_Products_Stations_StationId",
                table: "Products",
                column: "StationId",
                principalTable: "Stations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Stations_StationId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_StationId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StationId",
                table: "Products");
        }
    }
}