using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MacharPos.Core.Entities;

public class DailyClose
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid StationId { get; set; }
    public Station? Station { get; set; }

    /// <summary>
    /// Business date being closed (date-only). Store as timestamp WITHOUT time zone (or date).
    /// </summary>
    [Required]
    public DateTime CloseDate { get; set; } // date-only

    // Snapshot totals
    [Column(TypeName = "numeric(18,2)")]
    public decimal TotalSales { get; set; }

    public int TotalTransactions { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal CashTotal { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal PosTotal { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal TransferTotal { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal TotalLpgKg { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal ActiveBatchRemainingKg { get; set; }

    // Cash count controls (optional)
    [Column(TypeName = "numeric(18,2)")]
    public decimal? CashCounted { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal? CashVariance { get; set; } // CashCounted - CashTotal

    public string? Notes { get; set; }

    // Closure info
    public bool IsClosed { get; set; } = false;

    public string? ClosedByUserId { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    // Super-admin modification audit
    public string? ModifiedBySuperAdminId { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModificationReason { get; set; }
}