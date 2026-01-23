using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MacharPos.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeStationToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HomeStationId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HomeStationId",
                table: "AspNetUsers");
        }
    }
}
