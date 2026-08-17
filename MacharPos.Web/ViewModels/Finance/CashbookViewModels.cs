using System.ComponentModel.DataAnnotations;
using MacharPos.Core.Entities;
using MacharPos.Web.ViewModels.Expenses;

namespace MacharPos.Web.ViewModels.Finance;

public sealed class CashbookFilterVm
{
    public string Period { get; set; } = "daily";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? StationId { get; set; }
}

public sealed class CashMovementCreateVm
{
    public Guid? StationId { get; set; }

    [Required]
    [Display(Name = "Date and time")]
    public DateTime OccurredAtLocal { get; set; }

    [Required]
    [Display(Name = "Movement type")]
    public CashMovementType Type { get; set; }

    [Display(Name = "Cash account")]
    public CashAccount Account { get; set; } = CashAccount.MainCash;

    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 3)]
    public string Description { get; set; } = "";

    [StringLength(200)]
    public string? Reference { get; set; }

    [Display(Name = "Destination station")]
    public Guid? DestinationStationId { get; set; }
}

public sealed class CashbookEntryVm
{
    public Guid? ManualMovementId { get; set; }
    public DateTime OccurredAtLocal { get; set; }
    public string StationName { get; set; } = "";
    public string Source { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Reference { get; set; }
    public string EnteredBy { get; set; } = "";
    public decimal MainCashImpact { get; set; }
    public decimal PettyCashImpact { get; set; }
    public bool CanReverse { get; set; }
}

public sealed class CashbookVm
{
    public CashbookFilterVm Filter { get; set; } = new();
    public CashMovementCreateVm NewMovement { get; set; } = new();
    public List<ExpenseStationOption> Stations { get; set; } = new();
    public List<CashbookEntryVm> Entries { get; set; } = new();
    public DateTime RangeStartLocal { get; set; }
    public DateTime RangeEndLocal { get; set; }
    public string PeriodLabel { get; set; } = "";
    public bool IsAdmin { get; set; }
    public decimal MainCashIn { get; set; }
    public decimal MainCashOut { get; set; }
    public decimal PettyCashIn { get; set; }
    public decimal PettyCashOut { get; set; }
    public decimal MainCashNet => MainCashIn - MainCashOut;
    public decimal PettyCashNet => PettyCashIn - PettyCashOut;
}

public sealed class CashReconciliationVm
{
    public Guid StationId { get; set; }
    public string StationName { get; set; } = "";
    public DateTime ReconciliationDate { get; set; }
    public List<ExpenseStationOption> Stations { get; set; } = new();
    public bool IsAdmin { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedAtLocal { get; set; }
    public string? ClosedBy { get; set; }

    public decimal OpeningMainCash { get; set; }
    public decimal OpeningPettyCash { get; set; }
    public decimal CashSales { get; set; }
    public decimal CashCreditCollections { get; set; }
    public decimal PaidMainCashExpenses { get; set; }
    public decimal PaidPettyCashExpenses { get; set; }
    public decimal ManualMainCashNet { get; set; }
    public decimal ManualPettyCashNet { get; set; }
    public decimal ExpectedMainCash { get; set; }
    public decimal ExpectedPettyCash { get; set; }

    public decimal? ActualMainCash { get; set; }
    public decimal? ActualPettyCash { get; set; }
    public decimal? MainCashVariance { get; set; }
    public decimal? PettyCashVariance { get; set; }
    public string? Notes { get; set; }
    public decimal ExpectedTotal => ExpectedMainCash + ExpectedPettyCash;
    public decimal? ActualTotal => ActualMainCash + ActualPettyCash;
    public decimal? TotalVariance => MainCashVariance + PettyCashVariance;
}
