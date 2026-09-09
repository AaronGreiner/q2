using Q2.Api.Features.Streaks;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.People;

/// <summary>
/// Someone using q2: the person signed in, and everybody they see in the app.
/// </summary>
/// <remarks>
/// This is <em>not</em> the account. Credentials live on
/// <c>Features.Accounts.AppUser</c>, which points here; a person can exist
/// without one, which is what lets a seeded world contain people nobody needs
/// to sign in as (docs/adr/0011-authentication-with-identity.md).
///
/// What this is instead is the identity the rest of the model points at,
/// because a shared goal, a chat message and a kudos all need to say *who*.
///
/// Streaks are not stored. They are derived from <see cref="DailyCheckIn"/>
/// rows by <see cref="StreakOn"/>, so the number can never disagree with the
/// days behind it.
/// </remarks>
public sealed class Person
{
    public const int MaxDisplayNameLength = 80;
    public const int MaxHandleLength = 40;
    public const int MaxInitialsLength = 2;

    /// <summary>
    /// How long an invite code may be.
    /// </summary>
    /// <remarks>
    /// Generous rather than exact: <see cref="Invites"/> makes them one length
    /// today, and a column that only fits today's format would have to be
    /// migrated the first time that changes.
    /// </remarks>
    public const int MaxInviteCodeLength = 64;

    /// <summary>How recently someone must have been seen to count as online.</summary>
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(5);

    private readonly List<DailyCheckIn> _checkIns = [];
    private readonly List<PersonBadge> _badges = [];

    // EF Core materialisation only.
    private Person()
    {
        DisplayName = string.Empty;
        Handle = string.Empty;
        Initials = string.Empty;
        AvatarColor = string.Empty;
    }

    private Person(Guid id, string displayName, string handle, string initials, string avatarColor)
    {
        Id = id;
        DisplayName = displayName;
        Handle = handle;
        Initials = initials;
        AvatarColor = avatarColor;
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; }

    /// <summary>The public handle, including the leading "@".</summary>
    public string Handle { get; private set; }

    /// <summary>One or two letters for the avatar.</summary>
    public string Initials { get; private set; }

    /// <summary>Hex colour of the avatar, chosen once so it stays recognisable.</summary>
    public string AvatarColor { get; private set; }

    /// <summary>
    /// The photograph this person uses instead of their initials, when they
    /// have chosen one.
    /// </summary>
    /// <remarks>
    /// A nullable id rather than a required picture, and
    /// <see cref="AvatarColor"/> and <see cref="Initials"/> stay filled in
    /// beside it. There is always something to draw: while the image is
    /// loading, when the person never uploaded one, and when they delete it —
    /// which is what stops a screen full of empty grey circles from being a
    /// normal state of the app.
    ///
    /// Not a foreign key with a cascade. Deleting the image is
    /// <see cref="Images.ImageService.DeleteAsync"/>'s job, and it clears this
    /// in the same transaction; a database-level cascade would do the same
    /// thing in a second place, invisibly.
    /// </remarks>
    public Guid? AvatarImageId { get; private set; }

    /// <summary>Total kudos this person has received, across their whole history.</summary>
    public int KudosReceived { get; private set; }

    /// <summary>How many goals this person has finished. Shown on the profile.</summary>
    public int GoalsCompleted { get; private set; }

    /// <summary>
    /// When this person was last active. There is no presence service, so this
    /// is written by the seeds and never updated at runtime — which is why
    /// <see cref="IsOnlineAt"/> takes the reference instant instead of reading
    /// a clock.
    /// </summary>
    public DateTimeOffset? LastSeenAt { get; private set; }

    /// <summary>
    /// The IANA zone this person's days are counted in — "Europe/Berlin".
    /// </summary>
    /// <remarks>
    /// Null means the zone configured for the deployment
    /// (<see cref="Q2.Api.Infrastructure.Time.TimeZoneResolver"/>), which is
    /// what every account starts with. It is a column rather than a constant
    /// because the moment a deadline decides whether somebody keeps a streak,
    /// "whatever the server thinks a day is" stops being good enough — and
    /// because the first person who travels must not lose one.
    ///
    /// Nothing sets it to anything else yet; there is no screen for it. What
    /// matters now is that every window in the database was computed against a
    /// zone that is written down.
    /// </remarks>
    public string? TimeZoneId { get; private set; }

    /// <summary>
    /// The unguessable half of this person's invite link. Null until they ask
    /// for one.
    /// </summary>
    /// <remarks>
    /// **Not the handle**, and that is the whole reason this column exists. A
    /// handle is public and searchable, so a link built from one would let
    /// anybody force a friendship on anybody else by guessing it. A code is
    /// random, is only ever seen by whoever the person sent it to, and can be
    /// replaced (<see cref="SetInviteCode"/>) the moment it ends up somewhere
    /// it should not be.
    ///
    /// Created on first use rather than at registration: a link nobody has
    /// asked for is a secret with no purpose, and generating one lazily means
    /// no backfill for the accounts that existed before this feature.
    /// </remarks>
    public string? InviteCode { get; private set; }

    public IReadOnlyList<DailyCheckIn> CheckIns => _checkIns;

    public IReadOnlyList<PersonBadge> Badges => _badges;

    /// <exception cref="DomainValidationException">Any invariant is violated.</exception>
    public static Person Create(
        Guid id,
        string displayName,
        string handle,
        string initials,
        string avatarColor)
    {
        var errors = new Dictionary<string, string[]>();

        var normalisedName = displayName?.Trim() ?? string.Empty;
        if (normalisedName.Length == 0)
        {
            errors[nameof(DisplayName)] = ["A display name is required."];
        }
        else if (normalisedName.Length > MaxDisplayNameLength)
        {
            errors[nameof(DisplayName)] = [$"A display name may be at most {MaxDisplayNameLength} characters long."];
        }

        var normalisedHandle = handle?.Trim() ?? string.Empty;
        if (normalisedHandle.Length == 0)
        {
            errors[nameof(Handle)] = ["A handle is required."];
        }
        else if (!normalisedHandle.StartsWith('@'))
        {
            errors[nameof(Handle)] = ["A handle must start with '@'."];
        }
        else if (normalisedHandle.Length > MaxHandleLength)
        {
            errors[nameof(Handle)] = [$"A handle may be at most {MaxHandleLength} characters long."];
        }

        var normalisedInitials = initials?.Trim() ?? string.Empty;
        if (normalisedInitials.Length == 0)
        {
            errors[nameof(Initials)] = ["Initials are required."];
        }

        if (!AvatarColors.IsValid(avatarColor))
        {
            errors[nameof(AvatarColor)] = ["An avatar colour must be a hex value such as '#6366f1'."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        return new Person(id, normalisedName, normalisedHandle, normalisedInitials, avatarColor);
    }

    /// <summary>
    /// Changes the name and the initials drawn beside it.
    /// </summary>
    /// <remarks>
    /// The two move together on purpose. Initials are derived from the name
    /// everywhere they are first created (<c>ProfileDefaults</c>), and letting
    /// a rename change one without the other is how "MS" ends up sitting next
    /// to "Jonas Weber" forever.
    /// </remarks>
    /// <exception cref="DomainValidationException">The name is empty or too long.</exception>
    public void Rename(string displayName, string initials)
    {
        var normalisedName = displayName?.Trim() ?? string.Empty;

        if (normalisedName.Length == 0)
        {
            throw new DomainValidationException(nameof(DisplayName), "A display name is required.");
        }

        if (normalisedName.Length > MaxDisplayNameLength)
        {
            throw new DomainValidationException(
                nameof(DisplayName),
                $"A display name may be at most {MaxDisplayNameLength} characters long.");
        }

        var normalisedInitials = initials?.Trim() ?? string.Empty;

        if (normalisedInitials.Length == 0)
        {
            throw new DomainValidationException(nameof(Initials), "Initials are required.");
        }

        DisplayName = normalisedName;
        Initials = normalisedInitials;
    }

    /// <summary>Sets the counters the profile screen shows. Seeding only.</summary>
    public void SetTotals(int kudosReceived, int goalsCompleted)
    {
        if (kudosReceived < 0 || goalsCompleted < 0)
        {
            throw new DomainValidationException(nameof(KudosReceived), "Totals cannot be negative.");
        }

        KudosReceived = kudosReceived;
        GoalsCompleted = goalsCompleted;
    }

    public void SetLastSeen(DateTimeOffset lastSeenAt) => LastSeenAt = lastSeenAt;

    /// <summary>Sets the zone this person's days are counted in.</summary>
    public void SetTimeZone(string? zoneId) =>
        TimeZoneId = string.IsNullOrWhiteSpace(zoneId) ? null : zoneId.Trim();

    /// <summary>
    /// Points this person's avatar at an image, or back at their initials when
    /// given null.
    /// </summary>
    /// <remarks>
    /// Takes an id rather than a <c>StoredImage</c>: whether that image exists,
    /// belongs to this person and was uploaded as an avatar is a question that
    /// needs the database, and it is answered by
    /// <see cref="Images.ImageService.RequireOwnedAsync"/> before this is
    /// called.
    /// </remarks>
    public void SetAvatarImage(Guid? imageId) =>
        AvatarImageId = imageId == Guid.Empty ? null : imageId;

    /// <summary>
    /// Gives this person a new invite link, replacing whatever they had.
    /// </summary>
    /// <remarks>
    /// The code is generated by the caller rather than here, for the same
    /// reason ids and timestamps are arguments everywhere in this model: a
    /// domain type that reached for a random number generator could not be
    /// tested twice with the same result (AGENTS.md section 4).
    ///
    /// Replacing is what makes a leaked link recoverable, and it is why the old
    /// one is simply overwritten: two live codes for one person would mean the
    /// one somebody wanted to revoke still worked.
    /// </remarks>
    /// <exception cref="DomainValidationException">The code is empty or too long.</exception>
    public void SetInviteCode(string code)
    {
        var trimmed = code?.Trim() ?? string.Empty;

        if (trimmed.Length is 0 or > MaxInviteCodeLength)
        {
            throw new DomainValidationException(nameof(InviteCode), "An invite code is required.");
        }

        InviteCode = trimmed;
    }

    public void ReceiveKudos() => KudosReceived++;

    public void WithdrawKudos() => KudosReceived = Math.Max(0, KudosReceived - 1);

    public bool IsOnlineAt(DateTimeOffset now) =>
        LastSeenAt is { } seen && now - seen <= OnlineWindow;

    /// <summary>
    /// Records that this person did something towards a goal on
    /// <paramref name="date"/>. Idempotent: a second check-in on the same day
    /// changes nothing, which is what keeps a streak a count of *days*.
    /// </summary>
    public void CheckIn(Guid id, DateOnly date)
    {
        if (_checkIns.Any(c => c.Date == date))
        {
            return;
        }

        _checkIns.Add(new DailyCheckIn(id, Id, date));
    }

    public void AwardBadge(Guid id, BadgeKey badge, DateOnly earnedOn)
    {
        if (_badges.Any(b => b.Badge == badge))
        {
            return;
        }

        _badges.Add(new PersonBadge(id, Id, badge, earnedOn));
    }

    /// <summary>Consecutive days of activity ending today, or yesterday.</summary>
    public int StreakOn(DateOnly today) => Streak.Count(_checkIns.Select(c => c.Date), today);

    /// <summary>Monday-to-Sunday activity for the week containing <paramref name="today"/>.</summary>
    public IReadOnlyList<bool> WeekActivity(DateOnly today) =>
        Streak.Week(_checkIns.Select(c => c.Date), today);
}

/// <summary>One day on which a person did something towards a goal.</summary>
/// <remarks>
/// The unit a streak is counted in. Storing days rather than a running total
/// means the number on screen is always recomputable, and a missed day cannot
/// leave a stale counter behind.
/// </remarks>
public sealed class DailyCheckIn
{
    // EF Core materialisation only.
    private DailyCheckIn()
    {
    }

    internal DailyCheckIn(Guid id, Guid personId, DateOnly date)
    {
        Id = id;
        PersonId = personId;
        Date = date;
    }

    public Guid Id { get; private set; }

    public Guid PersonId { get; private set; }

    public DateOnly Date { get; private set; }
}

/// <summary>The palette avatars are drawn from.</summary>
/// <remarks>
/// A closed set rather than a free-form colour: every value here has been
/// checked to carry white text at 4.5:1 or better, which a colour picked at
/// random could not promise.
/// </remarks>
public static class AvatarColors
{
    public const string Indigo = "#4f46e5";
    public const string Pink = "#db2777";
    public const string Amber = "#b45309";
    public const string Cyan = "#0e7490";
    public const string Violet = "#7c3aed";
    public const string Red = "#dc2626";
    public const string Teal = "#0f766e";
    public const string Orange = "#c2410c";
    public const string Blue = "#2563eb";
    public const string Green = "#15803d";

    public static readonly IReadOnlyList<string> All =
    [
        Indigo, Pink, Amber, Cyan, Violet, Red, Teal, Orange, Blue, Green,
    ];

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}
