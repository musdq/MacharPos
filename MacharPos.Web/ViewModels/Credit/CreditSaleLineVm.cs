using MacharPos.Core.Entities;
using MacharPos.Web.ViewModels.Sales;
using System.ComponentModel.DataAnnotations;

namespace MacharPos.Web.ViewModels.Credit;

public class CreditSaleLineVm
{
    [Required]
    public Guid ProductId { get; set; }

    // Same behavior as regular sales
    public SaleEntryMode EntryMode { get; set; } = SaleEntryMode.ByQty;

    [Range(0.01, 999999)]
    public decimal Qty { get; set; } = 1m;

    // Auto-calculated for LPG
    public decimal? Kg { get; set; }

    // Used in By Amount mode
    public decimal? Amount { get; set; }

    // Shown in UI and used for calculations
    public decimal UnitPrice { get; set; }
}