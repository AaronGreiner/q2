namespace Q2.Api.Features.Accounts;

/// <summary>
/// Every column in q2 that names a person, and where each goes when that
/// person asks for their data (#36).
/// </summary>
/// <remarks>
/// **One list, checked against the model.** Erasure walks the same tables
/// (<see cref="AccountService"/> and the database's cascades), and both used to
/// know them only implicitly. <c>PersonalDataInventoryTests</c> compares this
/// list with every property of the EF model that is a foreign key to
/// <c>Person</c> or ends in <c>PersonId</c>: a table added tomorrow fails the
/// build until it is placed here — exported, or left out with the reason
/// written next to it — and the export test then checks it has its section.
///
/// The export is the person's own share of each row (decided in the session):
/// what they wrote, made, chose and were sent; on their own photographs only how
/// many believed and doubted, and why, as the app shows them; never anybody
/// else's messages or pictures.
/// </remarks>
public static class PersonalData
{
    /// <summary>The columns, each with the section of the export it lands in.</summary>
    public static IReadOnlyList<PersonalColumn> Columns { get; } =
    [
        new("Person", "Id", "person"),
        new("AppUser", "PersonId", "account"),
        new("UserSettings", "PersonId", "settings"),
        new("DailyCheckIn", "PersonId", "checkIns"),
        new("PersonBadge", "PersonId", "badges"),
        new("Friendship", "RequesterId", "friendships"),
        new("Friendship", "AddresseeId", "friendships"),
        new("Block", "BlockerPersonId", "blocks"),
        new("Block", "BlockedPersonId", null, "Who blocked you is theirs to know: telling you would undo what a block is for."),
        new("Goal", "OwnerPersonId", "goals"),
        new("GoalParticipant", "PersonId", "checking"),
        new("GoalPause", "PersonId", "goals"),
        new("PauseVeto", "PersonId", "objections"),
        new("GoalChange", "PersonId", "goals"),
        new("GoalChangeChecker", "PersonId", "checking"),
        new("GoalChangeVeto", "PersonId", "objections"),
        new("GoalDraft", "OwnerPersonId", "drafts"),
        new("ProofPhoto", "UploaderPersonId", "goals"),
        new("ProofVote", "VoterPersonId", "votes"),
        new("ProofReaction", "PersonId", "reactions"),
        new("StoredImage", "OwnerPersonId", "images"),
        new("ChallengeEntry", "PersonId", "challengeEntries"),
        new("ChallengeReaction", "PersonId", "reactions"),
        new("ActivityEvent", "ActorPersonId", "activity"),
        new("ActivityKudos", "PersonId", "kudosGiven"),
        new("ConversationParticipant", "PersonId", "conversations"),
        new("ChatMessage", "SenderPersonId", "messages"),
        new("MessageReaction", "PersonId", "reactions"),
        new("Notification", "RecipientPersonId", "notifications"),
        new("Notification", "ActorPersonId", null, "A line in somebody else's bell that names you is theirs: what they were told."),
        new("PushSubscription", "PersonId", "devices"),
        new("Report", "ReporterPersonId", "reports"),
    ];

    /// <summary>The sections an export has, in the order it writes them.</summary>
    public static IReadOnlyList<string> Sections { get; } =
        [.. Columns.Select(column => column.Section).OfType<string>().Distinct()];
}

/// <summary>One column that names a person.</summary>
/// <param name="Entity">The entity's CLR name, as the EF model has it.</param>
/// <param name="Property">The property that holds the person's id.</param>
/// <param name="Section">The export's section for it; null when it is left out.</param>
/// <param name="LeftOutBecause">Why it is not exported, when it is not.</param>
public sealed record PersonalColumn(string Entity, string Property, string? Section, string? LeftOutBecause = null);
