using MacharPos.Core.Entities;

namespace MacharPos.Web.ViewModels.DailyClose;

public class DailyCloseVm
{
    public string StationName { get; set; } = "";
    public DateTime CloseDate { get; set; } // date-only

    public MacharPos.Core.Entities.DailyClose Existing { get; set; }

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
}