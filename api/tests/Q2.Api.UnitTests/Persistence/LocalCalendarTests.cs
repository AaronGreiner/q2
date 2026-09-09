using Q2.Api.Infrastructure.Time;

namespace Q2.Api.UnitTests.Persistence;

/// <summary>
/// The one place UTC instants and local days are converted into each other.
/// </summary>
/// <remarks>
/// Every assertion here is skipped when the platform has no zone database —
/// which is what <c>InvariantGlobalization</c> does on Windows. The deployment
/// target is Linux (docs/deployment.md), and a developer on Windows should not
/// be told their build is broken by a limitation of their operating system.
/// </remarks>
public class LocalCalendarTests
{
    private static readonly TimeZoneInfo? Berlin = Find("Europe/Berlin");

    [Fact]
    public void UtcIsAlwaysAvailableAndCountsPlainDays()
    {
        var calendar = new LocalCalendar(TimeZoneInfo.Utc);
        var day = new DateOnly(2026, 6, 1);

        Assert.Equal(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), calendar.StartOfDay(day));
        Assert.Equal(new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero), calendar.EndOfDay(day).AddTicks(1));
    }

    [Fact]
    public void TodayIsTheDayItIsWhereThePersonIs()
    {
        if (Berlin is null)
        {
            return;
        }

        var calendar = new LocalCalendar(Berlin);

        // 22:30 UTC on the last of May is already the first of June in Berlin.
        var instant = new DateTimeOffset(2026, 5, 31, 22, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 6, 1), calendar.Today(instant));
        Assert.Equal(new DateOnly(2026, 5, 31), new LocalCalendar(TimeZoneInfo.Utc).Today(instant));
    }

    /// <summary>
    /// A day is not always 24 hours long. The end of one has to be exactly the
    /// start of the next whatever the clocks did, or a second falls between two
    /// windows and a proof delivered in it counts for neither.
    /// </summary>
    [Theory]
    [InlineData(2026, 3, 29)]
    [InlineData(2026, 10, 25)]
    [InlineData(2026, 6, 1)]
    public void TheEndOfADayIsExactlyTheStartOfTheNext(int year, int month, int day)
    {
        if (Berlin is null)
        {
            return;
        }

        var calendar = new LocalCalendar(Berlin);
        var date = new DateOnly(year, month, day);

        Assert.Equal(calendar.StartOfDay(date.AddDays(1)), calendar.EndOfDay(date).AddTicks(1));
    }

    [Fact]
    public void TheShortDayIsTwentyThreeHoursAndTheLongOneTwentyFive()
    {
        if (Berlin is null)
        {
            return;
        }

        var calendar = new LocalCalendar(Berlin);

        var springForward = calendar.EndOfDay(new DateOnly(2026, 3, 29)).AddTicks(1)
            - calendar.StartOfDay(new DateOnly(2026, 3, 29));
        var fallBack = calendar.EndOfDay(new DateOnly(2026, 10, 25)).AddTicks(1)
            - calendar.StartOfDay(new DateOnly(2026, 10, 25));

        Assert.Equal(TimeSpan.FromHours(23), springForward);
        Assert.Equal(TimeSpan.FromHours(25), fallBack);
    }

    private static TimeZoneInfo? Find(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }
}
