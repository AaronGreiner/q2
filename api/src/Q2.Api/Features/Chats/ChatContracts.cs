using Q2.Api.Features.Goals;
using Q2.Api.Features.Images;
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
/// <param name="LastMessageHasPhoto">
/// Whether the last message carried a photograph — the preview's only clue
/// when there were no words with it.
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
/// then. Never a missed window, which is not news — the row shows what came
/// before it. Sent as a kind rather than as a sentence, because the sentence is
/// language.
/// </param>
/// <param name="GoalId">The goal this is the conversation of, for <see cref="ConversationKind.Goal"/>.</param>
/// <param name="IsMyGoal">
/// Whether that goal is the reader's own — the one thing that decides which
/// section of the list it belongs in: yours to deliver, or a friend's to check.
/// </param>
/// <param name="GoalCurrent">
/// The open window of the reader's own goal, so the row can say what comes next
/// ("heute fällig · noch 9 Std.") instead of repeating the last thing that went
/// wrong. Null for anybody else's goal and for every other conversation.
/// </param>
/// <param name="AwaitingMyVote">
/// Whether a photograph in it is waiting for the reader's verdict. Apart from
/// <paramref name="UnreadCount"/> on purpose: unread says "something new is
/// here", this says "something here is waiting for you".
/// </param>
/// <param name="GoalEnded">
/// Whether that goal has stopped — carried through or given up. The list folds
/// such conversations away at its end, so that after a year it is not mostly
/// finished things (#101). Still a conversation: it opens, and it can be
/// written in.
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
    bool LastMessageHasPhoto,
    bool LastMessageIsMine,
    bool LastMessageIsDeleted,
    DateTimeOffset? LastMessageAt,
    int UnreadCount,
    bool IsMuted,
    bool IsPinned,
    GoalEventKind? LastEvent,
    Guid? GoalId,
    bool IsMyGoal,
    bool AwaitingMyVote,
    GoalInstanceResponse? GoalCurrent,
    bool GoalEnded);

/// <summary>A reaction, rolled up: which kind, how many, and whether it is yours.</summary>
public sealed record MessageReactionResponse(KudosKind Kind, int Count, bool IsMine);

/// <summary>One message in a thread.</summary>
/// <param name="SenderName">
/// Only set when the client needs to draw it — a group message from somebody
/// else. Null for your own messages and for direct chats, where the header
/// already says who is speaking.
/// </param>
/// <param name="Text">The words; empty when the message is only a photograph.</param>
/// <param name="Image">
/// The photograph sent with it, with its dimensions so the thread can reserve
/// the space before the bytes arrive. Null for a message of words alone, and
/// for a photograph that has since been deleted.
/// </param>
/// <param name="EditedAt">When the words were last corrected, for the "bearbeitet" marker (#25).</param>
/// <param name="IsDeleted">
/// Taken back by its sender (#25). The words, the photograph and the reactions
/// are gone from the response as well as from the database; what is left is the
/// place in the thread, so nobody's reply hangs in the air.
/// </param>
/// <param name="CanEdit">
/// Whether the reader may still correct the words — their own message, inside
/// <see cref="ChatMessage.EditWindow"/> — so the app offers it exactly while
/// the server would take it.
/// </param>
public sealed record ChatMessageResponse(
    Guid Id,
    Guid SenderId,
    string? SenderName,
    string Text,
    ImageResponse? Image,
    bool IsMine,
    DateTimeOffset SentAt,
    IReadOnlyList<MessageReactionResponse> Reactions,
    DateTimeOffset? EditedAt,
    bool IsDeleted,
    bool CanEdit);

/// <summary>Request body for correcting a message's words.</summary>
public sealed record EditMessageRequest(string? Text = null);

/// <summary>The goal a conversation belongs to, drawn at the top of it.</summary>
/// <param name="Current">
/// The window that is open now, so the banner can say "noch 2 von 3" rather
/// than a percentage of nothing in particular — or, until it ends, the one that
/// was just delivered or relapsed (<c>Goal.ShownInstanceAt</c>). Null when the
/// goal is finished.
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
/// <param name="Description">
/// What counts, in the owner's words, when they said: the thread is where its
/// photographs are voted on, so the agreement is shown above them (#95).
/// </param>
public sealed record ChatPinnedGoalResponse(
    Guid Id,
    string Title,
    string? Description,
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
/// <param name="Day">
/// For a window: the local day it was due on. Separate from <paramref name="At"/>,
/// which is when the outcome was settled — a run of missed windows is often
/// settled in one go, the next time anybody looks.
/// </param>
/// <param name="Proof">
/// For a delivered photograph: the photograph itself, with the reader's vote and
/// whether they may still cast one — exactly what the vote screen shows.
/// </param>
/// <param name="Uses">For a logged use of a Don't: which one of its period it was.</param>
/// <param name="Limit">
/// For a Don't's window or logged use: the limit of its period. Null on a Do's
/// lines, which is how a client tells "Durchgehalten" from "Geschafft".
/// </param>
/// <param name="ImageId">For a logged use: its optional photograph.</param>
/// <param name="Change">
/// For an edit and what became of it: what changed, from what to what, and
/// the objection to it while that is still open.
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
    ProofResponse? Proof,
    DateOnly? Day,
    int? Uses,
    int? Limit,
    Guid? ImageId,
    GoalChangeResponse? Change);

/// <summary>Everything the thread screen shows, with the newest page of the thread.</summary>
/// <param name="Members">
/// Everybody in it, the reader included, for the sheet behind the header.
/// </param>
/// <param name="Messages">
/// The messages on the newest page, oldest first — not the whole thread
/// (<see cref="ThreadPage"/>).
/// </param>
/// <param name="Events">
/// In a goal's conversation, what happened to the goal on the same page,
/// oldest first — to be shown between the messages by time. Empty for every
/// other conversation.
/// </param>
/// <param name="OlderCursor">
/// What to ask <c>GET /api/chats/{id}/messages?before=</c> with for the page
/// before this one. Null when this page begins the thread.
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
    bool IsPinned,
    IReadOnlyList<GoalEventResponse> Events,
    IReadOnlyList<PersonSummary> Members,
    string? OlderCursor);

/// <summary>A page of a thread from before the one already shown.</summary>
/// <remarks>
/// Only the lines: who is in the thread, its goal and its name came with the
/// newest page and do not change by scrolling back.
/// </remarks>
/// <param name="Messages">The messages on this page, oldest first.</param>
/// <param name="Events">What happened to the goal on this page, oldest first.</param>
/// <param name="OlderCursor">
/// What to ask for the page before this one with. Null when this page begins
/// the thread.
/// </param>
public sealed record ChatPageResponse(
    IReadOnlyList<ChatMessageResponse> Messages,
    IReadOnlyList<GoalEventResponse> Events,
    string? OlderCursor);

/// <summary>Request body for sending a message.</summary>
/// <remarks>Nullable so an empty body produces a field error, not a binding failure.</remarks>
/// <param name="Text">The words. May be left out when a photograph is sent.</param>
/// <param name="ImageId">
/// A photograph uploaded beforehand to <c>/api/images?purpose=ChatPhoto</c>. It
/// has to be the sender's own, and it can be sent once.
/// </param>
public sealed record SendMessageRequest(string? Text = null, Guid? ImageId = null);

/// <summary>Request body for toggling a reaction.</summary>
/// <remarks>Nullable so an empty body produces a field error, not a binding failure.</remarks>
public sealed record ToggleReactionRequest(KudosKind? Kind = null);

/// <summary>Request body for muting a conversation, or letting it ring again.</summary>
/// <remarks>Nullable so an empty body produces a field error, not a binding failure.</remarks>
public sealed record MuteChatRequest(bool? Muted = null);

/// <summary>Request body for pinning a conversation to the top of its section (#24).</summary>
public sealed record PinChatRequest(bool? Pinned = null);

/// <summary>One photograph in a conversation, for its info sheet's grid (#24).</summary>
/// <param name="Id">The message or the proof it came with.</param>
/// <param name="IsProof">
/// A photograph delivered against the goal of a goal's conversation, rather
/// than one sent as a message. Proofs carry their <paramref name="Source"/>,
/// because every proof says whether it was live (docs/adr/0036-live-or-screenshot.md).
/// </param>
/// <param name="AuthorName">Who sent or delivered it, for the full-screen view.</param>
/// <param name="Media">
/// A proof may be a short clip (#124); everything else in a conversation is a
/// photograph.
/// </param>
/// <param name="PosterImageId">A clip's still, which is what its tile shows.</param>
/// <param name="InsetImageId">A proof's front-camera picture, for the full-screen view (#125).</param>
public sealed record ChatPhotoResponse(
    Guid Id,
    Guid ImageId,
    bool IsProof,
    string AuthorName,
    DateTimeOffset At,
    ProofSource? Source,
    MediaKind Media = MediaKind.Photo,
    Guid? PosterImageId = null,
    Guid? InsetImageId = null);

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
