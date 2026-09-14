using Q2.Api.Features.Notifications;

namespace Q2.Api.UnitTests.Notifications;

[Trait("Category", "Unit")]
public class NotificationPreferencesTests
{
    /// <summary>
    /// Somebody who never opened the screen has said nothing, and the defaults
    /// are their answer until they change it — every switch on, and quiet at
    /// night.
    /// </summary>
    [Fact]
    public void EverySwitchStartsOnAndSoDoQuietHours()
    {
        var defaults = NotificationPreferences.Default;

        Assert.All(Enum.GetValues<NotificationSwitch>(), key => Assert.True(defaults.Allows(key)));
        Assert.Equal(QuietHours.DefaultFrom, defaults.QuietHoursFrom);
        Assert.Equal(QuietHours.DefaultTo, defaults.QuietHoursTo);
    }

    [Fact]
    public void EachSwitchIsReadByItsOwnKeyAndOnlyThat()
    {
        var off = NotificationPreferences.Default with { GoalUpdates = false };

        Assert.False(off.Allows(NotificationSwitch.GoalUpdates));

        Assert.All(
            Enum.GetValues<NotificationSwitch>().Where(key => key != NotificationSwitch.GoalUpdates),
            key => Assert.True(off.Allows(key)));
    }

    /// <summary>Half a window is not a window.</summary>
    [Fact]
    public void AQuietWindowHasBothEndsOrNone()
    {
        var half = NotificationPreferences.Default.WithQuietHours(new TimeOnly(21, 0), null);

        Assert.Null(half.QuietHoursFrom);
        Assert.Null(half.QuietHoursTo);

        var whole = NotificationPreferences.Default.WithQuietHours(new TimeOnly(21, 0), new TimeOnly(6, 30));

        Assert.Equal(new TimeOnly(21, 0), whole.QuietHoursFrom);
        Assert.Equal(new TimeOnly(6, 30), whole.QuietHoursTo);
    }
}
