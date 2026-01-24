using System.ComponentModel.DataAnnotations;

namespace MacharPos.Web.ViewModels.Credit;

public class CreditSaleLineVm
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(0.01, 999999)]
    public decimal Qty { get; set; } = 1m;
}