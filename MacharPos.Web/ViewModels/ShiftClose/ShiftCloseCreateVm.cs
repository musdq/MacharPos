using System.ComponentModel.DataAnnotations;

namespace MacharPos.Web.ViewModels.ShiftClose;

public class ShiftCloseCreateVm
{
    [Required, MaxLength(30)]
    public string ShiftName { get; set; } = "Morning";

    // Optional: choose a business date (defaults to today)
    public DateTime? ShiftDate { get; set; }

    public string? Notes { get; set; }

    public string? HandedOverTo { get; set; }
}
