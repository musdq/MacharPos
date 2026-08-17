using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MacharPos.Core.Entities;

public enum ExpenseCategory
{
    [Display(Name = "Plant maintenance")]
    PlantMaintenance = 0,
    [Display(Name = "Vehicle maintenance")]
    VehicleMaintenance = 1,
    [Display(Name = "Miscellaneous")]
    Miscellaneous = 2,
    [Display(Name = "Office expense")]
    OfficeExpense = 3,
    [Display(Name = "Administration expense")]
    AdministrationExpense = 4
}

public enum ExpenseStatus
{
    [Display(Name = "Pending")]
    Pending = 0,
    [Display(Name = "Approved")]
    Approved = 1,
    [Display(Name = "Rejected")]
    Rejected = 2
}

public enum ExpensePaymentStatus
{
    [Display(Name = "Unpaid")]
    Unpaid = 0,
    [Display(Name = "Paid")]
    Paid = 1,
    [Display(Name = "Cancelled")]
    Cancelled = 2
}

public enum ExpensePaymentMethod
{
    [Display(Name = "Cash")]
    Cash = 0,
    [Display(Name = "POS")]
    POS = 1,
    [Display(Name = "Bank transfer")]
    BankTransfer = 2,
    [Display(Name = "Petty cash")]
    PettyCash = 3
}

public class ExpenseRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StationId { get; set; }
    public Station? Station { get; set; }

    public string RequestedByUserId { get; set; } = "";

    public DateTime ExpenseDateTimeUtc { get; set; }

    public string Reason { get; set; } = "";

    [Column(TypeName = "numeric(18,2)")]
    public decimal Amount { get; set; }

    public ExpenseCategory Category { get; set; }
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Pending;

    public string? EvidenceOriginalFileName { get; set; }
    public string? EvidenceContentType { get; set; }
    public long? EvidenceFileSize { get; set; }
    public byte[]? EvidenceData { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }

    public ExpensePaymentStatus PaymentStatus { get; set; } = ExpensePaymentStatus.Unpaid;
    public ExpensePaymentMethod? PaymentMethod { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public string? PaidByUserId { get; set; }
    public string? Payee { get; set; }
    public string? PaymentReference { get; set; }
    public string? PaymentNote { get; set; }

    public string? PaymentEvidenceOriginalFileName { get; set; }
    public string? PaymentEvidenceContentType { get; set; }
    public long? PaymentEvidenceFileSize { get; set; }
    public byte[]? PaymentEvidenceData { get; set; }

    public DateTime? PaymentCancelledAtUtc { get; set; }
    public string? PaymentCancelledByUserId { get; set; }
}
