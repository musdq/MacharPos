using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MacharPos.Core.Entities;

/// <summary>
/// Phase 2: Shift Close (Morning/Evening/Night etc.).
/// Used for mid-day reconciliation and cash handover.
/// </summary>
public class ShiftClose
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid StationId { get; set; }
    public Station? Station { get; set; }

    /// <summary>
    /// Business date (date-only). Stored as DATE.
    /// </summary>
    [Required]
    public DateTime ShiftDate { get; set; } // date-only

    [Required, MaxLength(30)]
    public string ShiftName { get; set; } = "Shift";

    /// <summary>
    /// Shift boundaries (UTC).
    /// </summary>
    [Required]
    public DateTime ShiftStartUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ShiftEndUtc { get; set; }

    // Snapshot totals
    [Column(TypeName = "numeric(18,2)")]
    public decimal TotalSales { get; set; }

    public int TotalTransactions { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal CashCollected { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal PosCollected { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal TransferCollected { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal TotalLpgKg { get; set; }

    // Credit stats within the shift window
    [Column(TypeName = "numeric(18,2)")]
    public decimal CreditIssuedTotal { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal CreditCollectedTotal { get; set; }

    // Cash count controls (optional)
    [Column(TypeName = "numeric(18,2)")]
    public decimal? CashCounted { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal? CashVariance { get; set; } // CashCounted - CashCollected

    public string? Notes { get; set; }
    public string? HandedOverTo { get; set; }

    // Closure info
    public bool IsClosed { get; set; } = false;
    public string? ClosedByUserId { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
}
