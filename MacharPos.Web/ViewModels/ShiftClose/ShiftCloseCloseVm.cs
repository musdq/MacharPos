using System.ComponentModel.DataAnnotations;

namespace MacharPos.Web.ViewModels.ShiftClose;

public class ShiftCloseCloseVm
{
    public Guid Id { get; set; }

    [Required, MaxLength(30)]
    public string ShiftName { get; set; } = "";

    public DateTime ShiftDate { get; set; }

    public DateTime ShiftStartUtc { get; set; }
    public DateTime? ShiftEndUtc { get; set; }

    // Read-only computed totals (display only)
    public decimal TotalSales { get; set; }
    public int TotalTransactions { get; set; }
    public decimal CashCollected { get; set; }
    public decimal PosCollected { get; set; }
    public decimal TransferCollected { get; set; }
    public decimal TotalLpgKg { get; set; }
    public decimal CreditIssuedTotal { get; set; }
    public decimal CreditCollectedTotal { get; set; }

    // User inputs
    [Range(0, 999999999)]
    public decimal? CashCounted { get; set; }

    public string? Notes { get; set; }
    public string? HandedOverTo { get; set; }
}
