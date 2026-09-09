using Q2.Api.Features.Notifications;

namespace Q2.Api.UnitTests.Notifications;

/// <summary>
/// The hours nothing is delivered.
/// </summary>
/// <remarks>
/// Crossing midnight is the normal case rather than the edge one — the default
/// is 22:00 to 07:00 — so it gets as many tests as the same-day window does.
/// </remarks>
public class QuietHoursTests
{
    private static readonly TimeOnly Ten = new(22, 0);

    private static readonly TimeOnly Seven = new(7, 0);

    [Theory]
    [InlineData(22, 0, true)]
    [InlineData(23, 30, true)]
    [InlineData(0, 0, true)]
    [InlineData(3, 15, true)]
    [InlineData(6, 59, true)]
    [InlineData(7, 0, false)]
    [InlineData(12, 0, false)]
    [InlineData(21, 59, false)]
    public void TheDefaultWindowCrossesMidnight(int hour, int minute, bool quiet)
    {
        Assert.Equal(quiet, QuietHours.Covers(Ten, Seven, new TimeOnly(hour, minute)));
    }

    [Theory]
    [InlineData(12, 0, false)]
    [InlineData(13, 0, true)]
    [InlineData(14, 30, true)]
    [InlineData(15, 0, false)]
    public void AWindowInsideOneDayIsHalfOpen(int hour, int minute, bool quiet)
    {
        // Inclusive at the start, exclusive at the end — so two windows that
        // meet cannot both claim the same minute.
        Assert.Equal(quiet, QuietHours.Covers(new TimeOnly(13, 0), new TimeOnly(15, 0), new TimeOnly(hour, minute)));
    }

    [Fact]
    public void NoWindowMeansNothingIsQuiet()
    {
        Assert.False(QuietHours.Covers(null, null, new TimeOnly(3, 0)));
        Assert.False(QuietHours.Covers(Ten, null, new TimeOnly(3, 0)));
        Assert.False(QuietHours.Covers(null, Seven, new TimeOnly(3, 0)));
    }

    /// <summary>
    /// A person could set this by accident, and reading it as twenty-four
    /// silent hours would be experienced as the feature being broken.
    /// </summary>
    [Fact]
    public void AWindowOfNoLengthIsSwitchedOff()
    {
        Assert.False(QuietHours.Covers(Ten, Ten, new TimeOnly(22, 0)));
        Assert.False(QuietHours.Covers(Ten, Ten, new TimeOnly(3, 0)));
    }

    /// <summary>
    /// The two evening rules have to meet rather than cancel: the risk warning
    /// never goes out before eight, and quiet hours start at ten.
    /// </summary>
    [Fact]
    public void TheEveningWarningStillHasItsWindow()
    {
        Assert.False(QuietHours.Covers(
            QuietHours.DefaultFrom,
            QuietHours.DefaultTo,
            new TimeOnly(Q2.Api.Features.Goals.GoalRisk.AlertHour, 0)));

        Assert.False(QuietHours.Covers(QuietHours.DefaultFrom, QuietHours.DefaultTo, new TimeOnly(21, 59)));
        Assert.True(QuietHours.Covers(QuietHours.DefaultFrom, QuietHours.DefaultTo, new TimeOnly(22, 0)));
    }
}
