using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MacharPos.Core.Entities;

public class CashReconciliation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid StationId { get; set; }
    public Station? Station { get; set; }

    [Required]
    public DateTime ReconciliationDate { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal OpeningMainCash { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal OpeningPettyCash { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal CashSales { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal CashCreditCollections { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal PaidMainCashExpenses { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal PaidPettyCashExpenses { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal ManualMainCashNet { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal ManualPettyCashNet { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal ExpectedMainCash { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal ExpectedPettyCash { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal ActualMainCash { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal ActualPettyCash { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal MainCashVariance { get; set; }
    [Column(TypeName = "numeric(18,2)")]
    public decimal PettyCashVariance { get; set; }

    public string? Notes { get; set; }
    public bool IsClosed { get; set; }
    public string? ClosedByUserId { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? ReopenedByUserId { get; set; }
    public DateTime? ReopenedAtUtc { get; set; }
    public string? ReopenReason { get; set; }
}
