using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MacharPos.Core.Entities;

public class CreditPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SaleId { get; set; }
    public Sale? Sale { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

    public string? ReceivedByUserId { get; set; }

    public string? Note { get; set; }
}