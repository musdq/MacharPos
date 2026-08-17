using MacharPos.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MacharPos.Web.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260809120000_AddCashbookAndReconciliation")]
public partial class AddCashbookAndReconciliation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(name: "PaidAtUtc", table: "ExpenseRequests", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "PaidByUserId", table: "ExpenseRequests", type: "character varying(450)", maxLength: 450, nullable: true);
        migrationBuilder.AddColumn<string>(name: "Payee", table: "ExpenseRequests", type: "character varying(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "PaymentCancelledAtUtc", table: "ExpenseRequests", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "PaymentCancelledByUserId", table: "ExpenseRequests", type: "character varying(450)", maxLength: 450, nullable: true);
        migrationBuilder.AddColumn<string>(name: "PaymentEvidenceContentType", table: "ExpenseRequests", type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<byte[]>(name: "PaymentEvidenceData", table: "ExpenseRequests", type: "bytea", nullable: true);
        migrationBuilder.AddColumn<long>(name: "PaymentEvidenceFileSize", table: "ExpenseRequests", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<string>(name: "PaymentEvidenceOriginalFileName", table: "ExpenseRequests", type: "character varying(255)", maxLength: 255, nullable: true);
        migrationBuilder.AddColumn<int>(name: "PaymentMethod", table: "ExpenseRequests", type: "integer", nullable: true);
        migrationBuilder.AddColumn<string>(name: "PaymentNote", table: "ExpenseRequests", type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>(name: "PaymentReference", table: "ExpenseRequests", type: "character varying(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<int>(name: "PaymentStatus", table: "ExpenseRequests", type: "integer", nullable: false, defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "CashMovements",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                StationId = table.Column<Guid>(type: "uuid", nullable: false),
                OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Type = table.Column<int>(type: "integer", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                MainCashImpact = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                PettyCashImpact = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                EnteredByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CounterpartyStationId = table.Column<Guid>(type: "uuid", nullable: true),
                TransferGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                ReversesMovementId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CashMovements", x => x.Id);
                table.ForeignKey(name: "FK_CashMovements_CashMovements_ReversesMovementId", column: x => x.ReversesMovementId, principalTable: "CashMovements", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_CashMovements_Stations_CounterpartyStationId", column: x => x.CounterpartyStationId, principalTable: "Stations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_CashMovements_Stations_StationId", column: x => x.StationId, principalTable: "Stations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CashReconciliations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                StationId = table.Column<Guid>(type: "uuid", nullable: false),
                ReconciliationDate = table.Column<DateTime>(type: "date", nullable: false),
                OpeningMainCash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                OpeningPettyCash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                CashSales = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                CashCreditCollections = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                PaidMainCashExpenses = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                PaidPettyCashExpenses = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                ManualMainCashNet = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                ManualPettyCashNet = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                ExpectedMainCash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                ExpectedPettyCash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                ActualMainCash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                ActualPettyCash = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                MainCashVariance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                PettyCashVariance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                ClosedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ReopenedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                ReopenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ReopenReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CashReconciliations", x => x.Id);
                table.ForeignKey(name: "FK_CashReconciliations_Stations_StationId", column: x => x.StationId, principalTable: "Stations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_ExpenseRequests_PaymentStatus_PaidAtUtc", table: "ExpenseRequests", columns: new[] { "PaymentStatus", "PaidAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_CashMovements_CounterpartyStationId", table: "CashMovements", column: "CounterpartyStationId");
        migrationBuilder.CreateIndex(name: "IX_CashMovements_ReversesMovementId", table: "CashMovements", column: "ReversesMovementId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_CashMovements_StationId_OccurredAtUtc", table: "CashMovements", columns: new[] { "StationId", "OccurredAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_CashMovements_TransferGroupId", table: "CashMovements", column: "TransferGroupId");
        migrationBuilder.CreateIndex(name: "IX_CashReconciliations_StationId_ReconciliationDate", table: "CashReconciliations", columns: new[] { "StationId", "ReconciliationDate" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CashReconciliations");
        migrationBuilder.DropTable(name: "CashMovements");
        migrationBuilder.DropIndex(name: "IX_ExpenseRequests_PaymentStatus_PaidAtUtc", table: "ExpenseRequests");
        migrationBuilder.DropColumn(name: "PaidAtUtc", table: "ExpenseRequests");
        migrationBuilder.DropColumn(name: "PaidByUserId", table: "ExpenseRequests");
        migrationBuilder.DropColumn(name: "Payee", table: "ExpenseRequests");
        migrationBuilder.DropColumn(name: "PaymentCancelledAtUtc", table: "ExpenseRequests");
        migrationBuilder.DropColumn(name: "PaymentCancelledByUserId", table: "ExpenseRequests");
        migrationBuilder.DropColumn(name: "PaymentEvidenceContentType", table: "ExpenseRequests");
        migrationBuilder.DropColumn(name: "PaymentEvidenceData", table: "ExpenseRequests");
        migrationBuilder.DropColumn(name: "PaymentEvidenceFileSize", table: "ExpenseRequests");
        migrationBuilder.DropColumn(name: "PaymentEvidenceOriginalFileName", table: "ExpenseRequests");
        migrationBuilder.DropColumn(name: "PaymentMethod", table: "ExpenseRequests");
        migrationBuilder.DropColumn(name: "PaymentNote", table: "ExpenseRequests");
        migrationBuilder.DropColumn(name: "PaymentReference", table: "ExpenseRequests");
        migrationBuilder.DropColumn(name: "PaymentStatus", table: "ExpenseRequests");
    }
}
