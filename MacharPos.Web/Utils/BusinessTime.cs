using System;

namespace MacharPos.Web.Utils;

/// <summary>
/// Single source of truth for "business day" handling.
/// Business day = Nigeria local date (Africa/Lagos, WAT).
///
/// Notes:
/// - Persisted timestamps (e.g., Sale.CreatedAt) are UTC (timestamptz).
/// - Operators reason about dates in local station time.
/// - Use this helper to derive correct UTC [start, end) ranges.
///
/// Backward compatible: keeps older method names used by earlier patches.
/// </summary>
public static class BusinessTime
{
    // Prefer IANA on Linux; fall back to Windows ID when running on Windows.
    private static readonly TimeZoneInfo WAT = ResolveWAT();

    private static TimeZoneInfo ResolveWAT()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Lagos"); }
        catch
        {
            // Windows
            return TimeZoneInfo.FindSystemTimeZoneById("W. Central Africa Standard Time");
        }
    }

    /// <summary>Returns Nigeria local "now" (with time component).</summary>
    public static DateTime LocalNow()
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, WAT);

    /// <summary>Returns Nigeria local "today" as date-only (Kind = Unspecified).</summary>
    public static DateTime TodayLocalDate()
        => DateTime.SpecifyKind(LocalNow().Date, DateTimeKind.Unspecified);

    /// <summary>
    /// Converts a Nigeria local DateTime (Kind Unspecified) to UTC.
    /// </summary>
    public static DateTime ToUtc(DateTime localDateTime)
    {
        var dt = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(dt, WAT);
    }

    /// <summary>
    /// Converts a Nigeria local date (date-only) into UTC range [start, end).
    /// </summary>
    public static (DateTime UtcStart, DateTime UtcEndExclusive) UtcRangeForLocalDate(DateTime localDate)
    {
        var d = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);

        var localStart = d;
        var localEnd = d.AddDays(1);

        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, WAT);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, WAT);

        return (utcStart, utcEnd);
    }

    /// <summary>
    /// UTC range for Nigeria local date range [startDate, endDateInclusive] (inclusive end).
    /// </summary>
    public static (DateTime UtcStart, DateTime UtcEndExclusive) UtcRangeForLocalDates(DateTime startLocalDate, DateTime endLocalDateInclusive)
    {
        var start = DateTime.SpecifyKind(startLocalDate.Date, DateTimeKind.Unspecified);
        var endInc = DateTime.SpecifyKind(endLocalDateInclusive.Date, DateTimeKind.Unspecified);

        var (utcStart, _) = UtcRangeForLocalDate(start);

        // end exclusive is the start of the next local day after endInc
        var (_, utcEndExclusive) = UtcRangeForLocalDate(endInc.AddDays(1));

        return (utcStart, utcEndExclusive);
    }

    public static DateTime AsDateOnlyLocal(DateTime dt)
        => DateTime.SpecifyKind(dt.Date, DateTimeKind.Unspecified);

    // ------------------------------
    // Backward-compatible aliases
    // ------------------------------

    /// <summary>Alias for older code: Nigeria local "today" as date-only.</summary>
    public static DateTime TodayLocalDateOnly() => TodayLocalDate();

    /// <summary>Alias for older code: UTC range for a given local date-only.</summary>
    public static (DateTime UtcStart, DateTime UtcEndExclusive) UtcDayRangeForLocalDate(DateTime localDateOnly)
        => UtcRangeForLocalDate(localDateOnly);
}
