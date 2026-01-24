using System.ComponentModel.DataAnnotations;
using MacharPos.Core.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MacharPos.Web.ViewModels.Credit;

public class CreditPaymentVm
{
    public Guid SaleId { get; set; }

    public string ReceiptNo { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    public string? Note { get; set; }

    public List<SelectListItem> PaymentMethods { get; set; } = new();
}