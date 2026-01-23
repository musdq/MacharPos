using System.ComponentModel.DataAnnotations;
using MacharPos.Core.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MacharPos.Web.ViewModels.Sales;

public class SaleCreateVm
{
    public Guid? CustomerId { get; set; }
    [Required]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public List<SaleLineVm> Lines { get; set; } = new() { new SaleLineVm() };

    // dropdowns
    public List<SelectListItem> Customers { get; set; } = new();
    public List<SelectListItem> Products { get; set; } = new();
    public List<SelectListItem> PaymentMethods { get; set; } = new();

    [Range(0, 999999999)]
    public decimal DiscountAmount { get; set; } = 0m;

    public string? ManagerEmail { get; set; }
    public string? ManagerPassword { get; set; }

}