using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;

namespace Q2.Api.Features.Chats;

/// <summary>
/// A conversation as it appears in the list.
/// </summary>
/// <remarks>
/// <paramref name="Name"/>, <paramref name="Initials"/> and
/// <paramref name="AvatarColor"/> are resolved server-side: for a group they
/// come from the conversation, for a direct chat from the other person. The
/// client should not have to know that rule to draw a row.
/// </remarks>
/// <param name="LastMessageSenderName">
/// Who wrote the preview, when the client needs to say so — a group message
/// from somebody else. Null for your own messages and for direct chats.
/// </param>
/// <param name="LastMessageIsMine">
/// Sent as a flag rather than as the word "Du", because the word is language
/// and the app ships in more than one.
/// </param>
/// <param name="Icon">
/// A group's avatar, as an icon name from <see cref="ConversationIcons"/>.
/// Null for a direct chat, which is drawn as the other person.
/// </param>
/// <param name="AvatarImageId">
/// The other person's photograph in a direct chat, when they have one. Always
/// null for a group, which has an icon rather than a face.
/// </param>
/// <param name="IsMuted">
/// Whether you muted it. It still counts as unread; it only does not ring.
/// </param>
/// <param name="LastMessageAt">
/// When the last thing happened in it — a message, or in a goal's conversation
/// something that happened to the goal. What the list is sorted by.
/// </param>
/// <param name="LastEvent">
/// Set when the last thing in a goal's conversation was something that happened
/// to the goal rather than a message; <paramref name="LastMessage"/> is null
/// then. Sent as a kind rather than as a sentence, because the sentence is
/// language.
/// </param>
/// <param name="GoalId">The goal this is the conversation of, for <see cref="ConversationKind.Goal"/>.</param>
/// <param name="IsMyGoal">
/// Whether that goal is the reader's own — the one thing that decides which
/// section of the list it belongs in: yours to deliver, or a friend's to check.
/// </param>
/// <param name="AwaitingMyVote">
/// Whether a photograph in it is waiting for the reader's verdict. Apart from
/// <paramref name="UnreadCount"/> on purpose: unread says "something new is
/// here", this says "something here is waiting for you".
/// </param>
public sealed record ChatSummaryResponse(
    Guid Id,
    ConversationKind Kind,
    string Name,
    string Initials,
    string? Icon,
    string AvatarColor,
    Guid? AvatarImageId,
    bool IsOnline,
    string? LastMessage,
    string? LastMessageSenderName,
    bool LastMessageIsMine,
    DateTimeOffset? LastMessageAt,
    int UnreadCount,
    bool IsMuted,
    GoalEventKind? LastEvent,
    Guid? GoalId,
    bool IsMyGoal,
    bool AwaitingMyVote);

/// <summary>A reaction, rolled up: which kind, how many, and whether it is yours.</summary>
public sealed record MessageReactionResponse(KudosKind Kind, int Count, bool IsMine);

/// <summary>One message in a thread.</summary>
/// <param name="SenderName">
/// Only set when the client needs to draw it — a group message from somebody
/// else. Null for your own messages and for direct chats, where the header
/// already says who is speaking.
/// </param>
public sealed record ChatMessageResponse(
    Guid Id,
    Guid SenderId,
    string? SenderName,
    string Text,
    bool IsMine,
    DateTimeOffset SentAt,
    IReadOnlyList<MessageReactionResponse> Reactions);

/// <summary>The goal a conversation belongs to, drawn at the top of it.</summary>
/// <param name="Current">
/// The window that is open now, so the banner can say "noch 2 von 3" rather
/// than a percentage of nothing in particular. Null when the goal is finished.
/// </param>
/// <param name="PausedUntil">
/// The last local day a running pause covers, or null when the goal is not
/// paused. Only the day, deliberately: the reason and the objection belong on
/// the goal's own screen, one tap away through the title, and a chat is not
/// where somebody should be asked to judge a friend's illness.
/// </param>
/// <param name="IsMine">
/// Whether the reader owns it, which decides whether the thread offers the
/// camera. The server refuses a photograph from anybody else either way.
/// </param>
public sealed record ChatPinnedGoalResponse(
    Guid Id,
    string Title,
    Goals.GoalInstanceResponse? Current,
    int Streak,
    DateOnly? PausedUntil,
    bool IsMine);

/// <summary>
/// Something that happened to a goal, where it happened in its conversation.
/// </summary>
/// <param name="Key">Unique within the thread, for a client to key its rows on.</param>
/// <param name="ActorName">
/// Who did it, when it was somebody other than the reader. Null for the
/// reader's own doing (<paramref name="IsMine"/>) and for what time or a vote
/// decided, which has nobody to name.
/// </param>
/// <param name="Streak">For a kept window: the streak it brought the goal to.</param>
/// <param name="ConfirmedProofs">For a window: how many photographs were believed in it.</param>
/// <param name="RequiredProofs">For a window: how many it took.</param>
/// <param name="Until">For a pause: the last local day it covers. Never the reason.</param>
/// <param name="Proof">
/// For a delivered photograph: the photograph itself, with the reader's vote and
/// whether they may still cast one — exactly what the vote screen shows.
/// </param>
public sealed record GoalEventResponse(
    string Key,
    GoalEventKind Kind,
    DateTimeOffset At,
    string? ActorName,
    bool IsMine,
    int? Streak,
    int? ConfirmedProofs,
    int? RequiredProofs,
    DateOnly? Until,
    ProofResponse? Proof);

/// <summary>Everything the thread screen shows.</summary>
/// <param name="Events">
/// In a goal's conversation, what happened to the goal, oldest first — to be
/// shown between the messages by time. Empty for every other conversation.
/// </param>
public sealed record ChatDetailResponse(
    Guid Id,
    ConversationKind Kind,
    string Name,
    string Initials,
    string? Icon,
    string AvatarColor,
    Guid? AvatarImageId,
    bool IsOnline,
    int MemberCount,
    DateTimeOffset? OtherLastSeenAt,
    ChatPinnedGoalResponse? PinnedGoal,
    IReadOnlyList<ChatMessageResponse> Messages,
    bool IsMuted,
    IReadOnlyList<GoalEventResponse> Events);

/// <summary>Request body for sending a message.</summary>
/// <remarks>Nullable so an empty body produces a field error, not a binding failure.</remarks>
public sealed record SendMessageRequest(string? Text = null);

/// <summary>Request body for toggling a reaction.</summary>
/// <remarks>Nullable so an empty body produces a field error, not a binding failure.</remarks>
public sealed record ToggleReactionRequest(KudosKind? Kind = null);

/// <summary>Request body for muting a conversation, or letting it ring again.</summary>
/// <remarks>Nullable so an empty body produces a field error, not a binding failure.</remarks>
public sealed record MuteChatRequest(bool? Muted = null);

/// <summary>Request body for opening a direct conversation with somebody.</summary>
/// <remarks>
/// There is no "create" here on purpose. Two people have at most one direct
/// conversation, so asking for it twice has to mean the same thing twice —
/// otherwise tapping the message button from two screens leaves two threads
/// with half the history in each.
/// </remarks>
public sealed record StartDirectChatRequest(Guid? PersonId = null);

/// <summary>Request body for creating a group conversation.</summary>
/// <remarks>
/// No goal: a goal has its own conversation, opened with it, and a free group
/// is never about one ([0027](../../../../docs/adr/0027-goal-conversations.md)).
/// </remarks>
/// <param name="Icon">The group's avatar, from <see cref="ConversationIcons"/>.</param>
/// <param name="MemberIds">Who else is in it. You are added automatically.</param>
public sealed record CreateGroupChatRequest(
    string? Title = null,
    string? Icon = null,
    IReadOnlyList<Guid>? MemberIds = null);
