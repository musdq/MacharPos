using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MacharPos.Core.Entities;

public enum CashAccount
{
    [Display(Name = "Main cash")]
    MainCash = 0,
    [Display(Name = "Petty cash")]
    PettyCash = 1
}

public enum CashMovementType
{
    [Display(Name = "Bank deposit")]
    BankDeposit = 0,
    [Display(Name = "Petty-cash top-up")]
    PettyCashTopUp = 1,
    [Display(Name = "Station transfer out")]
    StationTransferOut = 2,
    [Display(Name = "Station transfer in")]
    StationTransferIn = 3,
    [Display(Name = "Other cash in")]
    OtherCashIn = 4,
    [Display(Name = "Other cash out")]
    OtherCashOut = 5,
    [Display(Name = "Reversal")]
    Reversal = 6
}

public class CashMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StationId { get; set; }
    public Station? Station { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public CashMovementType Type { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal MainCashImpact { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal PettyCashImpact { get; set; }

    public string Description { get; set; } = "";
    public string? Reference { get; set; }
    public string EnteredByUserId { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? CounterpartyStationId { get; set; }
    public Station? CounterpartyStation { get; set; }
    public Guid? TransferGroupId { get; set; }

    public Guid? ReversesMovementId { get; set; }
    public CashMovement? ReversesMovement { get; set; }
}
