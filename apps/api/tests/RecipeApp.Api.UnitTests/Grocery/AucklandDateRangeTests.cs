using FluentAssertions;
using RecipeApp.Api.Grocery;

namespace RecipeApp.Api.UnitTests.Grocery;

public sealed class AucklandDateRangeTests
{
    [Fact]
    public void ForDates_UsesTheDaylightSavingOffsetAtEachBoundary()
    {
        var range = AucklandDateRange.ForDates(
            new DateOnly(2026, 4, 5),
            new DateOnly(2026, 4, 5));

        range.StartUtc.Should().Be(new DateTime(2026, 4, 4, 11, 0, 0, DateTimeKind.Utc));
        range.EndExclusiveUtc.Should().Be(new DateTime(2026, 4, 5, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ForDates_DoesNotAssumeEveryCalendarDayIsTwentyFourHours()
    {
        var range = AucklandDateRange.ForDates(
            new DateOnly(2026, 9, 27),
            new DateOnly(2026, 9, 27));

        range.StartUtc.Should().Be(new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc));
        range.EndExclusiveUtc.Should().Be(new DateTime(2026, 9, 27, 11, 0, 0, DateTimeKind.Utc));
    }
}
