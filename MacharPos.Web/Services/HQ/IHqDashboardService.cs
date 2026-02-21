using MacharPos.Web.ViewModels.HQ;

namespace MacharPos.Web.Services.HQ;

public interface IHqDashboardService
{
    /// <summary>
    /// Builds HQ KPIs for the specified Nigeria-local business date (date-only).
    /// </summary>
    Task<HqDashboardVm> BuildAsync(DateTime reportDateLocal);
}
