namespace SmartHealthMonitoring.Common;

public static class AppointmentTime
{
    public static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    public static DateTime NowVietnam => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);

    public static DateTime ToUtc(DateTime vietnamLocal) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(vietnamLocal, DateTimeKind.Unspecified), VietnamTimeZone);

    public static DateTime ToVietnam(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), VietnamTimeZone);

    public static (DateTime StartUtc, DateTime EndUtc) GetUtcDayRange(DateOnly localDate)
    {
        var startLocal = localDate.ToDateTime(TimeOnly.MinValue);
        return (ToUtc(startLocal), ToUtc(startLocal.AddDays(1)));
    }

    public static (DateTime StartUtc, DateTime EndUtc) GetUtcDateRange(DateOnly startLocalDate, DateOnly endLocalDate)
    {
        var startLocal = startLocalDate.ToDateTime(TimeOnly.MinValue);
        var endExclusiveLocal = endLocalDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return (ToUtc(startLocal), ToUtc(endExclusiveLocal));
    }
}
