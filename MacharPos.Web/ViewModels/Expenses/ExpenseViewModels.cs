using System.ComponentModel.DataAnnotations;
using MacharPos.Core.Entities;
using Microsoft.AspNetCore.Http;

namespace MacharPos.Web.ViewModels.Expenses;

public sealed class ExpenseRequestCreateVm
{
    [Required]
    [Display(Name = "Expense date and time")]
    public DateTime ExpenseDateTimeLocal { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = "";

    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal Amount { get; set; }

    [Required]
    public ExpenseCategory Category { get; set; }

    [Display(Name = "Upload proof")]
    public IFormFile? EvidenceFile { get; set; }

    [Display(Name = "Take a photo")]
    public IFormFile? EvidencePhoto { get; set; }
}

public sealed class ExpenseListRowVm
{
    public Guid Id { get; set; }
    public DateTime ExpenseDateTimeLocal { get; set; }
    public DateTime CreatedAtLocal { get; set; }
    public string Reason { get; set; } = "";
    public decimal Amount { get; set; }
    public ExpenseCategory Category { get; set; }
    public ExpenseStatus Status { get; set; }
    public string StationName { get; set; } = "";
    public string RequestedByUserId { get; set; } = "";
    public string RequesterName { get; set; } = "";
    public bool HasEvidence { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime? ReviewedAtLocal { get; set; }
    public string? ReviewNote { get; set; }
    public ExpensePaymentStatus PaymentStatus { get; set; }
    public ExpensePaymentMethod? PaymentMethod { get; set; }
    public DateTime? PaidAtLocal { get; set; }
    public string? PayerName { get; set; }
    public string? Payee { get; set; }
    public string? PaymentReference { get; set; }
    public string? PaymentNote { get; set; }
    public bool HasPaymentEvidence { get; set; }
}

public sealed class ExpenseRequestPageVm
{
    public ExpenseRequestCreateVm Form { get; set; } = new();
    public List<ExpenseListRowVm> MyRequests { get; set; } = new();
}

public sealed record ExpenseStationOption(Guid Id, string Name);

public sealed class ExpenseManagementVm
{
    public List<ExpenseListRowVm> Items { get; set; } = new();
    public List<ExpenseStationOption> Stations { get; set; } = new();
    public ExpenseStatus? StatusFilter { get; set; }
    public Guid? StationId { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public decimal PendingAmount { get; set; }
    public int ApprovedUnpaidCount { get; set; }
    public decimal ApprovedUnpaidAmount { get; set; }
    public int PaidCount { get; set; }
    public decimal PaidAmount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed class ExpenseReportFilterVm
{
    public string Period { get; set; } = "monthly";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? StationId { get; set; }
    public ExpenseCategory? Category { get; set; }
    public ExpenseStatus? Status { get; set; }
    public ExpensePaymentStatus? PaymentStatus { get; set; }
}

public sealed class ExpenseReportVm
{
    public ExpenseReportFilterVm Filter { get; set; } = new();
    public List<ExpenseListRowVm> Items { get; set; } = new();
    public List<ExpenseStationOption> Stations { get; set; } = new();
    public DateTime RangeStartLocal { get; set; }
    public DateTime RangeEndLocal { get; set; }
    public string PeriodLabel { get; set; } = "";
    public decimal ApprovedAmount { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal RejectedAmount { get; set; }
    public int ApprovedCount { get; set; }
    public int PendingCount { get; set; }
    public int RejectedCount { get; set; }
    public decimal PaidAmount { get; set; }
    public int PaidCount { get; set; }
    public decimal ApprovedUnpaidAmount { get; set; }
    public int ApprovedUnpaidCount { get; set; }
}

public sealed class ExpensePaymentVm
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [Display(Name = "Payment method")]
    public ExpensePaymentMethod PaymentMethod { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Payee { get; set; } = "";

    [StringLength(500)]
    [Display(Name = "Payment note")]
    public string? PaymentNote { get; set; }

    [Display(Name = "Payment proof")]
    public IFormFile? PaymentEvidence { get; set; }

    public ExpenseStatus? ReturnStatus { get; set; }
    public Guid? ReturnStationId { get; set; }
    public int ReturnPage { get; set; } = 1;
}
