using System.ComponentModel.DataAnnotations;

namespace MacharPos.Web.ViewModels.Sales;

public class SaleLineVm
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(0.01, 999999)]
    public decimal Qty { get; set; } = 1m;

    [Range(0, 999999999)]
    public decimal UnitPrice { get; set; } = 0m;
}