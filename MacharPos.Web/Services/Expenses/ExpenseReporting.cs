using MacharPos.Core.Entities;
using MacharPos.Web.Utils;

namespace MacharPos.Web.Services.Expenses;

public sealed record ExpenseReportRange(
    string Period,
    string Label,
    DateTime StartLocal,
    DateTime EndLocal,
    DateTime UtcStart,
    DateTime UtcEndExclusive);

public static class ExpenseReporting
{
    public static ExpenseReportRange ResolveRange(
        string? period,
        DateTime? customStart,
        DateTime? customEnd)
    {
        var normalized = (period ?? "monthly").Trim().ToLowerInvariant();
        var today = BusinessTime.TodayLocalDate();

        DateTime start;
        DateTime end;
        string label;

        switch (normalized)
        {
            case "daily":
                start = today;
                end = today;
                label = "Daily";
                break;

            case "weekly":
                var daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                start = today.AddDays(-daysSinceMonday);
                end = start.AddDays(6);
                label = "Weekly";
                break;

            case "yearly":
                start = new DateTime(today.Year, 1, 1);
                end = new DateTime(today.Year, 12, 31);
                label = "Yearly";
                break;

            case "custom":
                start = (customStart ?? today).Date;
                end = (customEnd ?? start).Date;
                if (end < start)
                    (start, end) = (end, start);
                label = "Custom";
                break;

            default:
                normalized = "monthly";
                start = new DateTime(today.Year, today.Month, 1);
                end = start.AddMonths(1).AddDays(-1);
                label = "Monthly";
                break;
        }

        start = DateTime.SpecifyKind(start.Date, DateTimeKind.Unspecified);
        end = DateTime.SpecifyKind(end.Date, DateTimeKind.Unspecified);

        var (utcStart, _) = BusinessTime.UtcRangeForLocalDate(start);
        var (utcEndExclusive, _) = BusinessTime.UtcRangeForLocalDate(end.AddDays(1));

        return new ExpenseReportRange(normalized, label, start, end, utcStart, utcEndExclusive);
    }

    public static string CategoryName(ExpenseCategory category) => category switch
    {
        ExpenseCategory.PlantMaintenance => "Plant maintenance",
        ExpenseCategory.VehicleMaintenance => "Vehicle maintenance",
        ExpenseCategory.Miscellaneous => "Miscellaneous",
        ExpenseCategory.OfficeExpense => "Office expense",
        ExpenseCategory.AdministrationExpense => "Administration expense",
        _ => category.ToString()
    };

    public static string StatusName(ExpenseStatus status) => status switch
    {
        ExpenseStatus.Pending => "Pending",
        ExpenseStatus.Approved => "Approved",
        ExpenseStatus.Rejected => "Rejected",
        _ => status.ToString()
    };
}
