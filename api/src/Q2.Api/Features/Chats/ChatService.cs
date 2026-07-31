using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Chats;

/// <summary>
/// Conversations: the list, one thread, and writing into it.
/// </summary>
/// <remarks>
/// A direct conversation has no name of its own, so every read has to resolve
/// "who is the other person" before it can produce a row. That happens here,
/// once, rather than in each client.
/// </remarks>
public sealed class ChatService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    IIdGenerator idGenerator,
    TimeProvider timeProvider,
    ILogger<ChatService> logger)
{
    public async Task<IReadOnlyList<ChatSummaryResponse>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var term = search?.Trim();

        var conversations = await LoadMineAsync(me.Id, cancellationToken);
        var people = await LoadPeopleAsync(conversations, cancellationToken);

        var summaries = new List<ChatSummaryResponse>(conversations.Count);

        foreach (var conversation in conversations)
        {
            var identity = ResolveIdentity(conversation, people, me.Id, now);
            var last = conversation.Messages.OrderBy(m => m.SentAt).ThenBy(m => m.Id).LastOrDefault();

            if (!string.IsNullOrEmpty(term)
                && !identity.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            summaries.Add(new ChatSummaryResponse(
                conversation.Id,
                conversation.Kind,
                identity.Name,
                identity.Initials,
                identity.AvatarColor,
                identity.IsOnline,
                last?.Text,

                // Only a group names the sender; in a direct chat the row is
                // already the other person.
                last is not null && conversation.Kind == ConversationKind.Group && last.SenderPersonId != me.Id
                    ? people.GetValueOrDefault(last.SenderPersonId)?.DisplayName
                    : null,
                last?.SenderPersonId == me.Id,
                last?.SentAt,
                conversation.UnreadCountFor(me.Id)));
        }

        // Newest conversation first, and a brand-new empty one before an old
        // silent one — which is what "sorted by last activity" has to mean when
        // there has not been any yet.
        return
        [
            .. summaries
                .OrderByDescending(s => s.LastMessageAt ?? DateTimeOffset.MinValue)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>
    /// One thread, and marking it read.
    /// </summary>
    /// <remarks>
    /// Opening a conversation <em>is</em> reading it, so the read marker moves
    /// here rather than through a separate call the client could forget to
    /// make — leaving a badge on a chat somebody is looking at.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">No conversation with that id exists.</exception>
    public async Task<ChatDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var conversation = await LoadOneAsync(id, cancellationToken);
        EnsureParticipant(conversation, me.Id);

        conversation.MarkRead(me.Id, now);
        await database.SaveChangesAsync(cancellationToken);

        return await DescribeAsync(conversation, me.Id, now, cancellationToken);
    }

    /// <exception cref="ResourceNotFoundException">No conversation with that id exists.</exception>
    /// <exception cref="DomainValidationException">The message is empty or too long.</exception>
    public async Task<ChatDetailResponse> SendAsync(Guid id, SendMessageRequest request, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var conversation = await LoadOneAsync(id, cancellationToken);
        EnsureParticipant(conversation, me.Id);

        conversation.AddMessage(idGenerator.NewId(), me.Id, request.Text ?? string.Empty, now);
        conversation.MarkRead(me.Id, now);
        await database.SaveChangesAsync(cancellationToken);

        // The text is the most personal thing q2 stores — the id and nothing
        // else (docs/privacy.md).
        logger.LogInformation("Message sent in conversation {ConversationId}", conversation.Id);

        return await DescribeAsync(conversation, me.Id, now, cancellationToken);
    }

    /// <exception cref="ResourceNotFoundException">No such conversation or message.</exception>
    /// <exception cref="DomainValidationException">That reaction is not available.</exception>
    public async Task<ChatDetailResponse> ToggleReactionAsync(
        Guid id,
        Guid messageId,
        ToggleReactionRequest request,
        CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var conversation = await LoadOneAsync(id, cancellationToken);
        EnsureParticipant(conversation, me.Id);

        var message = conversation.Messages.SingleOrDefault(m => m.Id == messageId)
            ?? throw new ResourceNotFoundException("Message", messageId);

        message.ToggleReaction(idGenerator.NewId(), me.Id, request.Emoji ?? string.Empty);
        await database.SaveChangesAsync(cancellationToken);

        return await DescribeAsync(conversation, me.Id, now, cancellationToken);
    }

    private async Task<ChatDetailResponse> DescribeAsync(
        Conversation conversation,
        Guid meId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var people = await LoadPeopleAsync([conversation], cancellationToken);
        var identity = ResolveIdentity(conversation, people, meId, now);

        var goal = conversation.GoalId is { } goalId
            ? await database.Goals.AsNoTracking().SingleOrDefaultAsync(g => g.Id == goalId, cancellationToken)
            : null;

        var messages = conversation.Messages
            .OrderBy(m => m.SentAt)
            .ThenBy(m => m.Id)
            .Select(m => new ChatMessageResponse(
                m.Id,
                m.SenderPersonId,

                // Only a group needs to say who is speaking; in a direct chat
                // the header already does, and repeating it above every bubble
                // is noise.
                conversation.Kind == ConversationKind.Group && m.SenderPersonId != meId
                    ? people.GetValueOrDefault(m.SenderPersonId)?.DisplayName
                    : null,
                m.Text,
                m.SenderPersonId == meId,
                m.SentAt,
                SummariseReactions(m, meId)))
            .ToList();

        var other = conversation.Kind == ConversationKind.Direct ? OtherPerson(conversation, people, meId) : null;

        return new ChatDetailResponse(
            conversation.Id,
            conversation.Kind,
            identity.Name,
            identity.Initials,
            identity.AvatarColor,
            identity.IsOnline,
            conversation.Participants.Count,
            other?.LastSeenAt,
            goal is null ? null : new ChatPinnedGoalResponse(goal.Id, goal.Title, goal.ProgressPercent),
            messages);
    }

    private static IReadOnlyList<MessageReactionResponse> SummariseReactions(ChatMessage message, Guid meId) =>
    [
        .. message.Reactions
            .GroupBy(r => r.Emoji)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new MessageReactionResponse(
                group.Key,
                group.Count(),
                group.Any(r => r.PersonId == meId))),
    ];

    private static Person? OtherPerson(
        Conversation conversation,
        IReadOnlyDictionary<Guid, Person> people,
        Guid meId) =>
        conversation.Participants
            .Where(p => p.PersonId != meId)
            .Select(p => people.GetValueOrDefault(p.PersonId))
            .FirstOrDefault(p => p is not null);

    /// <summary>
    /// The name, avatar and presence a conversation is shown with: its own for
    /// a group, the other person's for a direct chat.
    /// </summary>
    private static (string Name, string Initials, string AvatarColor, bool IsOnline) ResolveIdentity(
        Conversation conversation,
        IReadOnlyDictionary<Guid, Person> people,
        Guid meId,
        DateTimeOffset now)
    {
        if (conversation.Kind == ConversationKind.Group)
        {
            return (
                conversation.Title ?? string.Empty,
                conversation.Emoji ?? "#",

                // A group is not a person and has no avatar colour of its own;
                // the client renders it in the accent colour instead.
                AvatarColors.Teal,
                false);
        }

        var other = OtherPerson(conversation, people, meId);

        return other is null
            ? (string.Empty, "?", AvatarColors.Teal, false)
            : (other.DisplayName, other.Initials, other.AvatarColor, other.IsOnlineAt(now));
    }

    private async Task<List<Conversation>> LoadMineAsync(Guid meId, CancellationToken cancellationToken) =>
        await database.Conversations
            .AsNoTracking()
            .Include(c => c.Participants)
            .Include(c => c.Messages)
            .Where(c => c.Participants.Any(p => p.PersonId == meId))
            .ToListAsync(cancellationToken);

    private async Task<Conversation> LoadOneAsync(Guid id, CancellationToken cancellationToken) =>
        await database.Conversations
            .Include(c => c.Participants)
            .Include(c => c.Messages)
            .ThenInclude(m => m.Reactions)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
        ?? throw new ResourceNotFoundException("Conversation", id);

    /// <exception cref="ResourceNotFoundException">
    /// Deliberately "not found" rather than "forbidden": telling somebody that
    /// a conversation exists but is not theirs is itself a disclosure.
    /// </exception>
    private static void EnsureParticipant(Conversation conversation, Guid meId)
    {
        if (conversation.Participants.All(p => p.PersonId != meId))
        {
            throw new ResourceNotFoundException("Conversation", conversation.Id);
        }
    }

    private async Task<IReadOnlyDictionary<Guid, Person>> LoadPeopleAsync(
        IReadOnlyCollection<Conversation> conversations,
        CancellationToken cancellationToken)
    {
        var ids = conversations
            .SelectMany(c => c.Participants.Select(p => p.PersonId).Concat(c.Messages.Select(m => m.SenderPersonId)))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, Person>();
        }

        return await database.People
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
    }
}
