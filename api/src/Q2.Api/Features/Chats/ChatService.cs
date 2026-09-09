using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Accounts;
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
    FriendsService friends,
    BlockList blockList,
    IIdGenerator idGenerator,
    TimeProvider timeProvider,
    ILogger<ChatService> logger)
{
    /// <summary>The icon a group gets when none was chosen.</summary>
    public const string DefaultGroupIcon = ConversationIcons.Default;

    public async Task<IReadOnlyList<ChatSummaryResponse>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var term = search?.Trim();

        var conversations = await LoadMineAsync(me.Id, cancellationToken);
        var hidden = await blockList.HiddenFromMeAsync(cancellationToken);

        /*
         * A direct thread with somebody blocked is gone from the list, not
         * deleted from the database.
         *
         * Blocking is reversible and erasing is not: lifting it puts the thread
         * back where it was. Destroying somebody's copy of a shared history is
         * what deleting an account does, and it is not a decision one person
         * gets to make about another's.
         *
         * A group both of them are in stays. It is other people's conversation
         * too, and quietly removing one member's half of it would rewrite
         * something that is not only yours — leaving is the answer there, and
         * it is a decision rather than a side effect.
         */
        conversations = [.. conversations.Where(conversation => !IsHiddenDirect(conversation, me.Id, hidden))];

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
                identity.Icon,
                identity.AvatarColor,
                identity.AvatarImageId,
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
        await EnsureVisibleAsync(conversation, me.Id, cancellationToken);

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
        await EnsureVisibleAsync(conversation, me.Id, cancellationToken);

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
        await EnsureVisibleAsync(conversation, me.Id, cancellationToken);

        var message = conversation.Messages.SingleOrDefault(m => m.Id == messageId)
            ?? throw new ResourceNotFoundException("Message", messageId);

        message.ToggleReaction(idGenerator.NewId(), me.Id, request.Kind ?? default);
        await database.SaveChangesAsync(cancellationToken);

        return await DescribeAsync(conversation, me.Id, now, cancellationToken);
    }

    /// <summary>
    /// Opens the direct conversation with somebody, creating it the first time.
    /// </summary>
    /// <remarks>
    /// Idempotent, and it has to be: the message button appears on the friends
    /// screen, on a feed row and on a goal's team list, and all three
    /// have to land in the same thread.
    ///
    /// Only friends. A stranger being able to open a thread with anybody is
    /// how a self-care app becomes a place people are shouted at.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">No such person.</exception>
    /// <exception cref="DomainValidationException">You are not friends with them.</exception>
    public async Task<ChatDetailResponse> StartDirectAsync(
        StartDirectChatRequest request,
        CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (request.PersonId is not { } personId || personId == Guid.Empty)
        {
            throw new DomainValidationException(nameof(request.PersonId), "A person is required.");
        }

        if (personId == me.Id)
        {
            throw new DomainValidationException(nameof(request.PersonId), "You cannot start a chat with yourself.");
        }

        if (!await database.People.AnyAsync(p => p.Id == personId, cancellationToken))
        {
            throw new ResourceNotFoundException("Person", personId);
        }

        /*
         * Before the friendship check, and answering 404 rather than "you are
         * not friends".
         *
         * Blocking already ended the friendship, so the check below would
         * refuse this anyway — but with a different sentence for a blocked
         * person than for a stranger, and a refusal that reads differently is a
         * notification.
         */
        await EnsureNotHiddenAsync(personId, cancellationToken);

        var friendIds = await friends.FriendIdsAsync(me.Id, cancellationToken);

        if (!friendIds.Contains(personId))
        {
            throw new DomainValidationException(
                nameof(request.PersonId),
                "You can only write to people you are friends with.");
        }

        var existing = await database.Conversations
            .Include(c => c.Participants)
            .Include(c => c.Messages)
            .ThenInclude(m => m.Reactions)
            .Where(c => c.Kind == ConversationKind.Direct
                && c.Participants.Any(p => p.PersonId == me.Id)
                && c.Participants.Any(p => p.PersonId == personId))
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            existing.MarkRead(me.Id, now);
            await database.SaveChangesAsync(cancellationToken);

            return await DescribeAsync(existing, me.Id, now, cancellationToken);
        }

        var conversation = Conversation.CreateDirect(idGenerator.NewId(), goalId: null, now);
        conversation.AddParticipant(idGenerator.NewId(), me.Id, now);
        conversation.AddParticipant(idGenerator.NewId(), personId);

        database.Conversations.Add(conversation);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Direct conversation {ConversationId} started", conversation.Id);

        return await DescribeAsync(conversation, me.Id, now, cancellationToken);
    }

    /// <summary>
    /// Creates a group conversation.
    /// </summary>
    /// <remarks>
    /// Unlike a direct chat this is not idempotent, and should not be: two
    /// groups with the same people and the same name are two different groups,
    /// which is exactly what somebody who made one for a different purpose
    /// wanted.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">A pinned goal that is not yours.</exception>
    /// <exception cref="DomainValidationException">The request is not usable.</exception>
    public async Task<ChatDetailResponse> CreateGroupAsync(
        CreateGroupChatRequest request,
        CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var memberIds = (request.MemberIds ?? []).Where(id => id != me.Id).Distinct().ToList();

        if (memberIds.Count == 0)
        {
            throw new DomainValidationException(nameof(request.MemberIds), "A group needs somebody else in it.");
        }

        if (memberIds.Count + 1 > Conversation.MaxParticipants)
        {
            throw new DomainValidationException(
                nameof(request.MemberIds),
                $"A group may have at most {Conversation.MaxParticipants} people in it.");
        }

        var friendIds = (await friends.FriendIdsAsync(me.Id, cancellationToken)).ToHashSet();

        if (memberIds.Any(id => !friendIds.Contains(id)))
        {
            throw new DomainValidationException(
                nameof(request.MemberIds),
                "A group can only be made up of your friends.");
        }

        if (request.GoalId is { } goalId)
        {
            var goal = await database.Goals
                .AsNoTracking()
                .Include(g => g.Participants)
                .SingleOrDefaultAsync(g => g.Id == goalId, cancellationToken);

            if (goal is null || !goal.IsVisibleTo(me.Id))
            {
                throw new ResourceNotFoundException("Goal", goalId);
            }

            var visibleTo = goal.Participants.Select(participant => participant.PersonId).ToHashSet();
            visibleTo.Add(goal.OwnerPersonId);

            if (memberIds.Any(memberId => !visibleTo.Contains(memberId)))
            {
                throw new DomainValidationException(
                    nameof(request.GoalId),
                    "A goal can only be pinned in a group whose members may see it.");
            }
        }

        var icon = string.IsNullOrWhiteSpace(request.Icon) ? DefaultGroupIcon : request.Icon.Trim();

        // Conversation.CreateGroup enforces the title and the icon itself, so
        // an empty one becomes a 400 without a second copy of the rule here.
        var conversation = Conversation.CreateGroup(
            idGenerator.NewId(),
            request.Title ?? string.Empty,
            icon,
            request.GoalId,
            now);

        conversation.AddParticipant(idGenerator.NewId(), me.Id, now);

        foreach (var memberId in memberIds)
        {
            conversation.AddParticipant(idGenerator.NewId(), memberId);
        }

        database.Conversations.Add(conversation);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Group conversation {ConversationId} created with {MemberCount} member(s)",
            conversation.Id,
            conversation.Participants.Count);

        return await DescribeAsync(conversation, me.Id, now, cancellationToken);
    }

    /// <summary>
    /// Leaves a group.
    /// </summary>
    /// <remarks>
    /// The last person out takes the conversation with them: an empty thread
    /// nobody can open is not something to keep, and the messages in it are
    /// personal data with no remaining reason to be stored (docs/privacy.md).
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">No such conversation of yours.</exception>
    /// <exception cref="DomainValidationException">A direct conversation cannot be left.</exception>
    public async Task LeaveAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        var conversation = await LoadOneAsync(id, cancellationToken);
        EnsureParticipant(conversation, me.Id);

        conversation.RemoveParticipant(me.Id);

        if (conversation.Participants.Count == 0)
        {
            database.Conversations.Remove(conversation);
        }

        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Left conversation {ConversationId}", id);
    }

    private async Task<ChatDetailResponse> DescribeAsync(
        Conversation conversation,
        Guid meId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var people = await LoadPeopleAsync([conversation], cancellationToken);
        var identity = ResolveIdentity(conversation, people, meId, now);

        // Conversation membership does not grant goal access. The creation
        // path prevents a mismatch, and this predicate also protects legacy or
        // otherwise inconsistent rows already present in the database.
        var goal = conversation.GoalId is { } goalId
            ? await database.Goals
                .AsNoTracking()
                .Include(g => g.Instances)
                .ThenInclude(instance => instance.Proofs)
                .Include(g => g.Pauses)
                .SingleOrDefaultAsync(
                    g => g.Id == goalId
                        && (g.OwnerPersonId == meId || g.Participants.Any(p => p.PersonId == meId)),
                    cancellationToken)
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
            identity.Icon,
            identity.AvatarColor,
            identity.AvatarImageId,
            identity.IsOnline,
            conversation.Participants.Count,
            other?.LastSeenAt,
            goal is null
                ? null
                : new ChatPinnedGoalResponse(
                    goal.Id,
                    goal.Title,
                    goal.CurrentInstance is { } window ? GoalInstanceResponse.From(window) : null,
                    goal.Streak,
                    goal.ActivePauseAt(now)?.EndsOn),
            messages);
    }

    private static IReadOnlyList<MessageReactionResponse> SummariseReactions(ChatMessage message, Guid meId) =>
    [
        .. message.Reactions
            .GroupBy(r => r.Kind)
            .OrderBy(group => group.Key)
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
    private static ChatIdentity ResolveIdentity(
        Conversation conversation,
        IReadOnlyDictionary<Guid, Person> people,
        Guid meId,
        DateTimeOffset now)
    {
        if (conversation.Kind == ConversationKind.Group)
        {
            return new ChatIdentity(
                conversation.Title ?? string.Empty,

                // Initials are what a client without the icon set falls back
                // to, and they come from the name for the same reason a
                // person's do.
                ProfileDefaults.Initials(conversation.Title),
                conversation.Icon ?? ConversationIcons.Default,

                // A group is not a person and has no avatar colour of its own;
                // the client renders it on a neutral surface instead.
                AvatarColors.Teal,

                // And no photograph: a group's avatar is its icon, and lending
                // it one member's face would say something untrue about it.
                null,
                false);
        }

        var other = OtherPerson(conversation, people, meId);

        return other is null
            ? new ChatIdentity(string.Empty, "?", null, AvatarColors.Teal, null, false)
            : new ChatIdentity(
                other.DisplayName,
                other.Initials,
                null,
                other.AvatarColor,
                other.AvatarImageId,
                other.IsOnlineAt(now));
    }

    /// <summary>
    /// What a conversation is drawn as, whichever kind it is.
    /// </summary>
    /// <remarks>
    /// A named record rather than a tuple: six positional values is where
    /// "which one was the colour again" starts costing more than the type.
    /// </remarks>
    private sealed record ChatIdentity(
        string Name,
        string Initials,
        string? Icon,
        string AvatarColor,
        Guid? AvatarImageId,
        bool IsOnline);

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

    /// <summary>
    /// Whether this is a direct thread with somebody who must not be seen.
    /// </summary>
    /// <remarks>
    /// Groups are deliberately not covered: a group is other people's
    /// conversation as well, and hiding it because one member was blocked would
    /// take a thread away from everybody who is still in it.
    /// </remarks>
    private static bool IsHiddenDirect(Conversation conversation, Guid meId, IReadOnlySet<Guid> hidden) =>
        conversation.Kind == ConversationKind.Direct
        && conversation.Participants.Any(participant =>
            participant.PersonId != meId && hidden.Contains(participant.PersonId));

    /// <summary>Refuses a hidden thread the way a thread that never existed is refused.</summary>
    private async Task EnsureVisibleAsync(Conversation conversation, Guid meId, CancellationToken cancellationToken)
    {
        if (IsHiddenDirect(conversation, meId, await blockList.HiddenFromMeAsync(cancellationToken)))
        {
            throw new ResourceNotFoundException("Conversation", conversation.Id);
        }
    }

    private async Task EnsureNotHiddenAsync(Guid personId, CancellationToken cancellationToken)
    {
        if (await blockList.IsHiddenFromMeAsync(personId, cancellationToken))
        {
            throw new ResourceNotFoundException("Person", personId);
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
