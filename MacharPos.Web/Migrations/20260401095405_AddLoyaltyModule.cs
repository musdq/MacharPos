using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MacharPos.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyDiscountAmount",
                table: "Sales",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyRefillsEarned",
                table: "Sales",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "LoyaltyRewardApplied",
                table: "Sales",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyRewardRedeemedCount",
                table: "Sales",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsLoyaltyEnabled",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyAvailableRewards",
                table: "Customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyLifetimeEarned",
                table: "Customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyLifetimeRedeemed",
                table: "Customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyStampCount",
                table: "Customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LoyaltyTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    StampDelta = table.Column<int>(type: "integer", nullable: false),
                    RewardDelta = table.Column<int>(type: "integer", nullable: false),
                    QualifyingRefillCount = table.Column<int>(type: "integer", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyTransactions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoyaltyTransactions_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LoyaltyTransactions_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_CustomerId",
                table: "LoyaltyTransactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_SaleId",
                table: "LoyaltyTransactions",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_StationId_CustomerId_CreatedAt",
                table: "LoyaltyTransactions",
                columns: new[] { "StationId", "CustomerId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoyaltyTransactions");

            migrationBuilder.DropColumn(
                name: "LoyaltyDiscountAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "LoyaltyRefillsEarned",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "LoyaltyRewardApplied",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "LoyaltyRewardRedeemedCount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "IsLoyaltyEnabled",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LoyaltyAvailableRewards",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LoyaltyLifetimeEarned",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LoyaltyLifetimeRedeemed",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LoyaltyStampCount",
                table: "Customers");
        }
    }
}
