using Q2.Api.Features.People;

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
    int UnreadCount);

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

/// <summary>The goal pinned to a thread, if there is one.</summary>
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
public sealed record ChatPinnedGoalResponse(
    Guid Id,
    string Title,
    Goals.GoalInstanceResponse? Current,
    int Streak,
    DateOnly? PausedUntil);

/// <summary>Everything the thread screen shows.</summary>
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
    IReadOnlyList<ChatMessageResponse> Messages);

/// <summary>Request body for sending a message.</summary>
/// <remarks>Nullable so an empty body produces a field error, not a binding failure.</remarks>
public sealed record SendMessageRequest(string? Text = null);

/// <summary>Request body for toggling a reaction.</summary>
/// <remarks>Nullable so an empty body produces a field error, not a binding failure.</remarks>
public sealed record ToggleReactionRequest(KudosKind? Kind = null);

/// <summary>Request body for opening a direct conversation with somebody.</summary>
/// <remarks>
/// There is no "create" here on purpose. Two people have at most one direct
/// conversation, so asking for it twice has to mean the same thing twice —
/// otherwise tapping the message button from two screens leaves two threads
/// with half the history in each.
/// </remarks>
public sealed record StartDirectChatRequest(Guid? PersonId = null);

/// <summary>Request body for creating a group conversation.</summary>
/// <param name="Icon">The group's avatar, from <see cref="ConversationIcons"/>.</param>
/// <param name="MemberIds">Who else is in it. You are added automatically.</param>
/// <param name="GoalId">Optional goal to pin the thread to.</param>
public sealed record CreateGroupChatRequest(
    string? Title = null,
    string? Icon = null,
    IReadOnlyList<Guid>? MemberIds = null,
    Guid? GoalId = null);
