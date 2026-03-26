using System.ComponentModel.DataAnnotations;

namespace MacharPos.Web.ViewModels.Sales;

using System.ComponentModel.DataAnnotations;

public enum SaleEntryMode
{
    ByQty = 1,
    ByAmount = 2
}

public class SaleLineVm
{
 
    public Guid? ProductId { get; set; }

   
    public decimal Qty { get; set; } = 1m;

    [Range(0, 999999999)]
    public decimal UnitPrice { get; set; } = 0m;

    [Required]
    public SaleEntryMode EntryMode { get; set; } = SaleEntryMode.ByQty;

    [Range(0, 999999999)]
    public decimal? Amount { get; set; }
}
