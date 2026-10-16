namespace Q2.Api.Features.Goals;

/// <summary>
/// One use a Don't's owner logged: "Heute 1 Bier".
/// </summary>
/// <remarks>
/// A row rather than a counter on the window, because each one is a line in the
/// goal's conversation (derived by <see cref="GoalTimeline"/>, never written as
/// a message) and may carry a photograph. Nobody votes on it and nobody is
/// notified of it: the owner said it themselves, and a confession that sets off
/// a round of pings is one people stop making.
/// </remarks>
public sealed class DontUse
{
    /// <summary>
    /// How long after logging a use its owner may take it back (#110).
    /// </summary>
    /// <remarks>
    /// For a slipped thumb, not for second thoughts: long enough for the
    /// "Rückgängig" the app shows for a few seconds to reach the server over a
    /// slow connection, short enough that the count stays an honest record.
    /// </remarks>
    public static readonly TimeSpan UndoPeriod = TimeSpan.FromSeconds(30);

    // EF Core materialisation only.
    private DontUse()
    {
    }

    internal DontUse(Guid id, Guid goalInstanceId, Guid? imageId, DateTimeOffset loggedAt)
    {
        Id = id;
        GoalInstanceId = goalInstanceId;
        ImageId = imageId;
        LoggedAt = loggedAt;
    }

    public Guid Id { get; private set; }

    public Guid GoalInstanceId { get; private set; }

    /// <summary>An optional photograph, read by the goal's audience like a proof.</summary>
    public Guid? ImageId { get; private set; }

    public DateTimeOffset LoggedAt { get; private set; }
}
