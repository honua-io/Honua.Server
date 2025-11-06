using System.Globalization;

namespace Honua.MapSDK.Utilities;

/// <summary>
/// Utility methods for working with time, dates, and temporal data.
/// </summary>
public static class TimeUtils
{
    private static readonly string[] CommonDateFormats = new[]
    {
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "MM/dd/yyyy",
        "dd/MM/yyyy",
        "yyyy/MM/dd",
        "MMMM dd, yyyy",
        "dd MMMM yyyy",
    };

    /// <summary>
    /// Parses a date string using common formats.
    /// </summary>
    /// <param name="dateString">Date string to parse.</param>
    /// <returns>Parsed DateTime, or null if parsing fails.</returns>
    public static DateTime? TryParseDate(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;

        // Try ISO 8601 first
        if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
            return result;

        // Try common formats
        foreach (var format in CommonDateFormats)
        {
            if (DateTime.TryParseExact(dateString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return result;
        }

        // Last resort: try with current culture
        if (DateTime.TryParse(dateString, out result))
            return result;

        return null;
    }

    /// <summary>
    /// Formats a DateTime as an ISO 8601 string.
    /// </summary>
    /// <param name="dateTime">DateTime to format.</param>
    /// <returns>ISO 8601 formatted string.</returns>
    public static string ToIso8601(DateTime dateTime)
    {
        return dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a duration in a human-readable format.
    /// </summary>
    /// <param name="duration">Duration to format.</param>
    /// <returns>Human-readable duration string (e.g., "2 hours 30 minutes").</returns>
    public static string FormatDuration(TimeSpan duration)
    {
        var parts = new List<string>();

        if (duration.Days > 0)
            parts.Add($"{duration.Days} {(duration.Days == 1 ? "day" : "days")}");

        if (duration.Hours > 0)
            parts.Add($"{duration.Hours} {(duration.Hours == 1 ? "hour" : "hours")}");

        if (duration.Minutes > 0)
            parts.Add($"{duration.Minutes} {(duration.Minutes == 1 ? "minute" : "minutes")}");

        if (duration.Seconds > 0 && duration.TotalMinutes < 5)
            parts.Add($"{duration.Seconds} {(duration.Seconds == 1 ? "second" : "seconds")}");

        return parts.Any() ? string.Join(" ", parts) : "0 seconds";
    }

    /// <summary>
    /// Formats a relative time string (e.g., "2 hours ago", "in 3 days").
    /// </summary>
    /// <param name="dateTime">DateTime to format.</param>
    /// <param name="relativeTo">Reference time (defaults to now).</param>
    /// <returns>Relative time string.</returns>
    public static string FormatRelativeTime(DateTime dateTime, DateTime? relativeTo = null)
    {
        var now = relativeTo ?? DateTime.UtcNow;
        var diff = now - dateTime;

        if (diff.TotalSeconds < 60)
            return "just now";

        if (diff.TotalMinutes < 60)
        {
            var minutes = (int)diff.TotalMinutes;
            return $"{minutes} {(minutes == 1 ? "minute" : "minutes")} ago";
        }

        if (diff.TotalHours < 24)
        {
            var hours = (int)diff.TotalHours;
            return $"{hours} {(hours == 1 ? "hour" : "hours")} ago";
        }

        if (diff.TotalDays < 30)
        {
            var days = (int)diff.TotalDays;
            return $"{days} {(days == 1 ? "day" : "days")} ago";
        }

        if (diff.TotalDays < 365)
        {
            var months = (int)(diff.TotalDays / 30);
            return $"{months} {(months == 1 ? "month" : "months")} ago";
        }

        var years = (int)(diff.TotalDays / 365);
        return $"{years} {(years == 1 ? "year" : "years")} ago";
    }

    /// <summary>
    /// Generates a range of dates between start and end.
    /// </summary>
    /// <param name="start">Start date.</param>
    /// <param name="end">End date.</param>
    /// <param name="interval">Interval between dates.</param>
    /// <returns>List of dates.</returns>
    public static List<DateTime> GenerateDateRange(DateTime start, DateTime end, TimeSpan interval)
    {
        var dates = new List<DateTime>();
        var current = start;

        while (current <= end)
        {
            dates.Add(current);
            current = current.Add(interval);
        }

        return dates;
    }

    /// <summary>
    /// Bins timestamps into intervals (e.g., hourly, daily).
    /// </summary>
    /// <param name="timestamps">Timestamps to bin.</param>
    /// <param name="interval">Bin interval.</param>
    /// <returns>Dictionary of bin start time to count of timestamps in that bin.</returns>
    public static Dictionary<DateTime, int> BinTimestamps(IEnumerable<DateTime> timestamps, TimeSpan interval)
    {
        var bins = new Dictionary<DateTime, int>();

        foreach (var timestamp in timestamps)
        {
            var binStart = RoundDown(timestamp, interval);
            if (!bins.ContainsKey(binStart))
                bins[binStart] = 0;
            bins[binStart]++;
        }

        return bins;
    }

    /// <summary>
    /// Rounds a DateTime down to the nearest interval.
    /// </summary>
    /// <param name="dateTime">DateTime to round.</param>
    /// <param name="interval">Rounding interval.</param>
    /// <returns>Rounded DateTime.</returns>
    public static DateTime RoundDown(DateTime dateTime, TimeSpan interval)
    {
        var ticks = dateTime.Ticks / interval.Ticks;
        return new DateTime(ticks * interval.Ticks, dateTime.Kind);
    }

    /// <summary>
    /// Rounds a DateTime up to the nearest interval.
    /// </summary>
    /// <param name="dateTime">DateTime to round.</param>
    /// <param name="interval">Rounding interval.</param>
    /// <returns>Rounded DateTime.</returns>
    public static DateTime RoundUp(DateTime dateTime, TimeSpan interval)
    {
        var ticks = (dateTime.Ticks + interval.Ticks - 1) / interval.Ticks;
        return new DateTime(ticks * interval.Ticks, dateTime.Kind);
    }

    /// <summary>
    /// Converts a Unix timestamp (seconds since epoch) to DateTime.
    /// </summary>
    /// <param name="unixTimestamp">Unix timestamp in seconds.</param>
    /// <returns>DateTime.</returns>
    public static DateTime FromUnixTimestamp(long unixTimestamp)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
    }

    /// <summary>
    /// Converts a DateTime to Unix timestamp (seconds since epoch).
    /// </summary>
    /// <param name="dateTime">DateTime to convert.</param>
    /// <returns>Unix timestamp in seconds.</returns>
    public static long ToUnixTimestamp(DateTime dateTime)
    {
        return new DateTimeOffset(dateTime).ToUnixTimeSeconds();
    }

    /// <summary>
    /// Gets the start of day for a given DateTime.
    /// </summary>
    /// <param name="dateTime">DateTime.</param>
    /// <returns>Start of day (00:00:00).</returns>
    public static DateTime StartOfDay(DateTime dateTime)
    {
        return dateTime.Date;
    }

    /// <summary>
    /// Gets the end of day for a given DateTime.
    /// </summary>
    /// <param name="dateTime">DateTime.</param>
    /// <returns>End of day (23:59:59.999).</returns>
    public static DateTime EndOfDay(DateTime dateTime)
    {
        return dateTime.Date.AddDays(1).AddTicks(-1);
    }

    /// <summary>
    /// Gets the start of week for a given DateTime.
    /// </summary>
    /// <param name="dateTime">DateTime.</param>
    /// <param name="firstDayOfWeek">First day of week (defaults to Sunday).</param>
    /// <returns>Start of week.</returns>
    public static DateTime StartOfWeek(DateTime dateTime, DayOfWeek firstDayOfWeek = DayOfWeek.Sunday)
    {
        var diff = (7 + (dateTime.DayOfWeek - firstDayOfWeek)) % 7;
        return dateTime.AddDays(-diff).Date;
    }

    /// <summary>
    /// Gets the start of month for a given DateTime.
    /// </summary>
    /// <param name="dateTime">DateTime.</param>
    /// <returns>Start of month.</returns>
    public static DateTime StartOfMonth(DateTime dateTime)
    {
        return new DateTime(dateTime.Year, dateTime.Month, 1, 0, 0, 0, dateTime.Kind);
    }

    /// <summary>
    /// Gets the end of month for a given DateTime.
    /// </summary>
    /// <param name="dateTime">DateTime.</param>
    /// <returns>End of month.</returns>
    public static DateTime EndOfMonth(DateTime dateTime)
    {
        return StartOfMonth(dateTime).AddMonths(1).AddTicks(-1);
    }

    /// <summary>
    /// Gets the start of year for a given DateTime.
    /// </summary>
    /// <param name="dateTime">DateTime.</param>
    /// <returns>Start of year.</returns>
    public static DateTime StartOfYear(DateTime dateTime)
    {
        return new DateTime(dateTime.Year, 1, 1, 0, 0, 0, dateTime.Kind);
    }

    /// <summary>
    /// Gets the end of year for a given DateTime.
    /// </summary>
    /// <param name="dateTime">DateTime.</param>
    /// <returns>End of year.</returns>
    public static DateTime EndOfYear(DateTime dateTime)
    {
        return StartOfYear(dateTime).AddYears(1).AddTicks(-1);
    }

    /// <summary>
    /// Checks if a DateTime falls within a date range.
    /// </summary>
    /// <param name="dateTime">DateTime to check.</param>
    /// <param name="start">Range start.</param>
    /// <param name="end">Range end.</param>
    /// <returns>True if within range; otherwise, false.</returns>
    public static bool IsInRange(DateTime dateTime, DateTime start, DateTime end)
    {
        return dateTime >= start && dateTime <= end;
    }

    /// <summary>
    /// Calculates business days between two dates (excluding weekends).
    /// </summary>
    /// <param name="start">Start date.</param>
    /// <param name="end">End date.</param>
    /// <returns>Number of business days.</returns>
    public static int CalculateBusinessDays(DateTime start, DateTime end)
    {
        if (start > end)
            (start, end) = (end, start);

        var days = 0;
        var current = start;

        while (current <= end)
        {
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
                days++;

            current = current.AddDays(1);
        }

        return days;
    }

    /// <summary>
    /// Common time interval presets.
    /// </summary>
    public static class Intervals
    {
        public static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan FifteenMinutes = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan ThirtyMinutes = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan Hour = TimeSpan.FromHours(1);
        public static readonly TimeSpan Day = TimeSpan.FromDays(1);
        public static readonly TimeSpan Week = TimeSpan.FromDays(7);
    }
}
