namespace RecipeApp.Api.Grocery;

public readonly record struct AucklandUtcRange(DateTime StartUtc, DateTime EndExclusiveUtc);

public static class AucklandDateRange
{
    private static readonly TimeZoneInfo AucklandTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");

    public static AucklandUtcRange ForDates(DateOnly from, DateOnly to)
    {
        if (from == DateOnly.MinValue || to == DateOnly.MaxValue || to < from)
        {
            throw new ArgumentOutOfRangeException(nameof(to), "The Auckland date range is invalid or outside supported boundaries.");
        }

        var localStart = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var localEnd = DateTime.SpecifyKind(
            to.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);

        return new AucklandUtcRange(
            TimeZoneInfo.ConvertTimeToUtc(localStart, AucklandTimeZone),
            TimeZoneInfo.ConvertTimeToUtc(localEnd, AucklandTimeZone));
    }

    public static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), AucklandTimeZone).DateTime);
}
