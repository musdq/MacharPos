using System.ComponentModel.DataAnnotations;
using MacharPos.Core.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MacharPos.Web.ViewModels.Credit;

public class CreditSaleCreateVm
{
    public Guid? CustomerId { get; set; }

    // Due date is optional; if omitted, treat as "due anytime"
    public DateTime? DueDate { get; set; }

    public string? Notes { get; set; }

    // Items (unit price is enforced from DB)
    public List<CreditSaleLineVm> Lines { get; set; } = new() { new CreditSaleLineVm() };

    // Optional initial payment (deposit)
    [Range(0, 999999999)]
    public decimal InitialPaymentAmount { get; set; } = 0m;

    public PaymentMethod InitialPaymentMethod { get; set; } = PaymentMethod.Cash;

    // dropdowns
    public List<SelectListItem> Customers { get; set; } = new();
    public List<SelectListItem> Products { get; set; } = new();
    public List<SelectListItem> PaymentMethods { get; set; } = new();
}