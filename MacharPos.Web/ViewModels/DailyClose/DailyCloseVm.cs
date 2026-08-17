using MacharPos.Core.Entities;

namespace MacharPos.Web.ViewModels.DailyClose;

public class DailyCloseVm
{
    public Guid StationId { get; set; }
    public string StationName { get; set; } = "";
    public DateTime CloseDate { get; set; } // date-only

    public MacharPos.Core.Entities.DailyClose? Existing { get; set; }

    // Live (recomputed) values for preview
    public decimal LiveTotalSales { get; set; }
    public int LiveTotalTransactions { get; set; }
    public decimal LiveCashTotal { get; set; }
    public decimal LivePosTotal { get; set; }
    public decimal LiveTransferTotal { get; set; }
    public decimal LiveTotalLpgKg { get; set; }
    public decimal LiveActiveBatchRemainingKg { get; set; }

    // Inputs
    public decimal? CashCounted { get; set; }
    public string? Notes { get; set; }

    // Super-admin edit
    public string? ModificationReason { get; set; }

    public decimal LiveCreditIssuedTotal { get; set; }
    public decimal LiveCreditCollectedTotal { get; set; }
    public decimal LiveCreditCollectedCash { get; set; }
    public decimal LiveCreditCollectedPos { get; set; }
    public decimal LiveCreditCollectedTransfer { get; set; }

    // Inputs (Tank scale)
    public decimal? TankClosingKg { get; set; }
    public decimal? TankReceiptsKg { get; set; } // optional

    // Live preview (computed)
    public decimal LiveTankOpeningKg { get; set; }
    public decimal LiveTankScaleSoldKg { get; set; }
    public decimal LiveTankVarianceKg { get; set; }

    // Optional: history rows shown inside same page
    public List<TankHistoryRowVm> TankHistory { get; set; } = new();
    public sealed record TankHistoryRowVm(
        DateTime CloseDate,
        decimal? OpeningKg,
        decimal ReceiptsKg,
        decimal? ClosingKg,
        decimal AppSoldKg,
        decimal? ScaleSoldKg,
        decimal? VarianceKg
    );

}
