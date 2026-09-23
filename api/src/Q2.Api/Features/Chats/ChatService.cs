using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Accounts;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Images;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;
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
/// once, rather than in each client. A goal's conversation is named after its
/// goal the same way, and shows what happened to the goal between its messages
/// — read off the goal, never written into the thread (<see cref="GoalTimeline"/>).
///
/// Every change here also reaches whoever is looking, through
/// <see cref="Notifier"/>: a message is a notification to the others and a
/// live update to the sender's own other devices, and reading a thread moves
/// the badge on all of the reader's. The chat list is where a message lives,
/// so none of this puts a line in the bell
/// ([0024](../../../../docs/adr/0024-one-notification-pipeline.md)).
/// </remarks>
public sealed class ChatService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    FriendsService friends,
    BlockList blockList,
    ImageService images,
    Notifier notifier,
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

        var goals = await LoadGoalsAsync(conversations, me.Id, cancellationToken);

        // A goal's conversation is listed only while its goal can be read. The
        // two memberships are the same set of people by construction; this is
        // the guard for a row that somehow is not.
        conversations = [.. conversations.Where(conversation => conversation.Kind != ConversationKind.Goal
            || (conversation.GoalId is { } goalId && goals.ContainsKey(goalId)))];

        var people = await LoadPeopleAsync(conversations, [], cancellationToken);

        // The one definition of "waiting for this person's verdict", shared
        // with the vote screen and its badge, so a row cannot say a photograph
        // is waiting that the vote screen does not show.
        var waitingInWindows = (await ProofService.WaitingForVote(database, me.Id, now)
            .Select(proof => proof.GoalInstanceId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var summaries = new List<ChatSummaryResponse>(conversations.Count);

        foreach (var conversation in conversations)
        {
            var goal = GoalOf(conversation, goals);
            var identity = ResolveIdentity(conversation, goal, people, me.Id, now);
            var last = conversation.Messages.OrderBy(m => m.SentAt).ThenBy(m => m.Id).LastOrDefault();

            if (!string.IsNullOrEmpty(term)
                && !identity.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // In a goal's conversation the newest thing may be something that
            // happened to the goal — a photograph, a missed window — and that
            // is what the row shows and is sorted by, not a quieter message
            // from before it.
            var lastEvent = goal is null ? null : GoalTimeline.For(goal, now)[^1];
            var eventIsNewest = lastEvent is not null && (last is null || lastEvent.At > last.SentAt);
            var message = eventIsNewest ? null : last;

            summaries.Add(new ChatSummaryResponse(
                conversation.Id,
                conversation.Kind,
                identity.Name,
                identity.Initials,
                identity.Icon,
                identity.AvatarColor,
                identity.AvatarImageId,
                identity.IsOnline,
                // A photograph without words has no preview text of its own;
                // the flag below is what the row says instead.
                string.IsNullOrEmpty(message?.Text) ? null : message.Text,

                // Only a conversation of more than two names the sender; in a
                // direct chat the row is already the other person.
                message is not null && conversation.Kind != ConversationKind.Direct && message.SenderPersonId != me.Id
                    ? people.GetValueOrDefault(message.SenderPersonId)?.DisplayName
                    : null,
                message?.ImageId is not null,
                message?.SenderPersonId == me.Id,
                eventIsNewest ? lastEvent!.At : last?.SentAt,
                conversation.UnreadCountFor(me.Id),
                conversation.IsMutedFor(me.Id),
                eventIsNewest ? lastEvent!.Kind : null,
                goal?.Id,
                goal?.OwnerPersonId == me.Id,
                goal is not null && goal.Instances.Any(instance => waitingInWindows.Contains(instance.Id))));
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

        // Read on the phone, cleared on the laptop too. Only the numbers move:
        // nothing on anybody's screen changed but the badge.
        notifier.Touch([me.Id]);
        await notifier.FlushAsync(cancellationToken);

        return await DescribeAsync(conversation, me.Id, now, cancellationToken);
    }

    /// <exception cref="ResourceNotFoundException">
    /// No conversation with that id exists, or the photograph is not the sender's.
    /// </exception>
    /// <exception cref="DomainValidationException">
    /// The message is empty or too long, or the photograph was already sent.
    /// </exception>
    public async Task<ChatDetailResponse> SendAsync(Guid id, SendMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var conversation = await LoadOneAsync(id, cancellationToken);
        EnsureParticipant(conversation, me.Id);
        await EnsureVisibleAsync(conversation, me.Id, cancellationToken);

        if (request.ImageId is { } imageId && imageId != Guid.Empty)
        {
            await EnsureSendablePhotoAsync(imageId, me.Id, cancellationToken);
        }

        var message = conversation.AddMessage(
            idGenerator.NewId(),
            me.Id,
            request.Text ?? string.Empty,
            now,
            request.ImageId);
        conversation.MarkRead(me.Id, now);

        // The goal's title is its conversation's name, and lives on the goal.
        var subject = conversation.Kind switch
        {
            ConversationKind.Group => conversation.Title,
            ConversationKind.Goal => await database.Goals
                .AsNoTracking()
                .Where(goal => goal.Id == conversation.GoalId)
                .Select(goal => goal.Title)
                .SingleOrDefaultAsync(cancellationToken),
            _ => null,
        };

        /*
         * Everybody else in it hears about it. Who has muted it, who is looking
         * right now and who is inside their quiet hours is decided on the way
         * out — a banner, a push or nothing — not here: to the chat list it is
         * an unread message whatever they chose (NotificationRules.InterruptionFor).
         *
         * The group's or the goal's name comes along so a lock screen can say
         * where; a direct chat is already named by its sender.
         */
        await notifier.StageAsync(
            new NotificationEvent(
                NotificationKind.MessageReceived,
                me.Id,
                NotificationTarget.Conversation,
                conversation.Id,
                Subject: subject,

                // A photograph alone has no words to quote; the lock screen
                // falls back to saying there is a new message.
                Excerpt: message.Text.Length > 0 ? message.Text : null),
            conversation.Participants.Select(participant => participant.PersonId),
            cancellationToken);

        await database.SaveChangesAsync(cancellationToken);

        // The sender's own other devices show it arriving too.
        notifier.Touch([me.Id], LiveArea.Chats, conversation.Id);
        await notifier.FlushAsync(cancellationToken);

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

        var hadReacted = message.Reactions.Any(reaction => reaction.PersonId == me.Id);
        message.ToggleReaction(idGenerator.NewId(), me.Id, request.Kind ?? default);
        var hasReacted = message.Reactions.Any(reaction => reaction.PersonId == me.Id);

        // Told once per person, not once for each of the three kinds they
        // tapped — and taken back again only if nothing of theirs is left on it.
        if (!hadReacted && hasReacted)
        {
            await notifier.StageAsync(
                new NotificationEvent(
                    NotificationKind.ReactionReceived,
                    me.Id,
                    NotificationTarget.Conversation,
                    conversation.Id),
                [message.SenderPersonId],
                cancellationToken);
        }
        else if (hadReacted && !hasReacted)
        {
            await notifier.RetractAsync(
                NotificationKind.ReactionReceived,
                message.SenderPersonId,
                me.Id,
                conversation.Id,
                cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);

        notifier.Touch(conversation.Participants.Select(participant => participant.PersonId), LiveArea.Chats, conversation.Id);
        await notifier.FlushAsync(cancellationToken);

        return await DescribeAsync(conversation, me.Id, now, cancellationToken);
    }

    /// <summary>
    /// Stops a conversation from ringing on this person's devices, or lets it
    /// ring again.
    /// </summary>
    /// <remarks>
    /// Theirs alone: everybody else in it hears it exactly as before, and
    /// nobody is told that somebody muted it.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">No such conversation of yours.</exception>
    /// <exception cref="DomainValidationException">The request does not say which.</exception>
    public async Task<ChatDetailResponse> SetMutedAsync(Guid id, MuteChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Muted is not { } muted)
        {
            throw new DomainValidationException(nameof(request.Muted), "Say whether to mute the conversation.");
        }

        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var conversation = await LoadOneAsync(id, cancellationToken);
        EnsureParticipant(conversation, me.Id);
        await EnsureVisibleAsync(conversation, me.Id, cancellationToken);

        conversation.SetMuted(me.Id, muted, now);
        await database.SaveChangesAsync(cancellationToken);

        notifier.Touch([me.Id], LiveArea.Chats, conversation.Id);
        await notifier.FlushAsync(cancellationToken);

        logger.LogInformation("Conversation {ConversationId} muted: {Muted}", conversation.Id, muted);

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

            notifier.Touch([me.Id]);
            await notifier.FlushAsync(cancellationToken);

            return await DescribeAsync(existing, me.Id, now, cancellationToken);
        }

        var conversation = Conversation.CreateDirect(idGenerator.NewId(), now);
        conversation.AddParticipant(idGenerator.NewId(), me.Id, now);
        conversation.AddParticipant(idGenerator.NewId(), personId);

        database.Conversations.Add(conversation);
        await database.SaveChangesAsync(cancellationToken);

        // An empty thread is not news, but it is a new row in both lists.
        notifier.Touch([me.Id, personId], LiveArea.Chats);
        await notifier.FlushAsync(cancellationToken);

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

        var icon = string.IsNullOrWhiteSpace(request.Icon) ? DefaultGroupIcon : request.Icon.Trim();

        // Conversation.CreateGroup enforces the title and the icon itself, so
        // an empty one becomes a 400 without a second copy of the rule here.
        var conversation = Conversation.CreateGroup(
            idGenerator.NewId(),
            request.Title ?? string.Empty,
            icon,
            now);

        conversation.AddParticipant(idGenerator.NewId(), me.Id, now);

        foreach (var memberId in memberIds)
        {
            conversation.AddParticipant(idGenerator.NewId(), memberId);
        }

        database.Conversations.Add(conversation);
        await database.SaveChangesAsync(cancellationToken);

        notifier.Touch(conversation.Participants.Select(participant => participant.PersonId), LiveArea.Chats);
        await notifier.FlushAsync(cancellationToken);

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
    /// <exception cref="DomainValidationException">
    /// A direct conversation, or a goal's, cannot be left (<see cref="Conversation.RemoveParticipant"/>).
    /// </exception>
    public async Task LeaveAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        var conversation = await LoadOneAsync(id, cancellationToken);
        EnsureParticipant(conversation, me.Id);

        conversation.RemoveParticipant(me.Id);

        var remaining = conversation.Participants.Select(participant => participant.PersonId).ToList();
        IReadOnlyList<Guid> removedPhotos = [];

        if (remaining.Count == 0)
        {
            removedPhotos = await images.RemoveChatPhotosAsync(
                conversation.Messages.Select(message => message.ImageId),
                cancellationToken);

            database.Conversations.Remove(conversation);
        }

        await database.SaveChangesAsync(cancellationToken);

        // Bytes last, once the rows are safely gone.
        await images.DeleteBytesAsync(removedPhotos, cancellationToken);

        // The thread is gone from this person's devices, so only their list
        // moves: asking the thread they just left to read itself again would
        // be a 404 under their thumb before the app has moved on. Everybody
        // else's member count moves with the thread itself.
        notifier.Touch([me.Id], LiveArea.Chats);
        notifier.Touch(remaining, LiveArea.Chats, id);
        await notifier.FlushAsync(cancellationToken);

        logger.LogInformation("Left conversation {ConversationId}", id);
    }

    private async Task<ChatDetailResponse> DescribeAsync(
        Conversation conversation,
        Guid meId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var goal = GoalOf(conversation, await LoadGoalsAsync([conversation], meId, cancellationToken));
        var timeline = goal is null ? [] : GoalTimeline.For(goal, now);
        var proofs = timeline.Select(entry => entry.Proof).OfType<ProofPhoto>().ToList();

        var proofPeopleIds = goal is null ? [] : ProofService.PeopleShownWith(proofs, [goal]);
        var people = await LoadPeopleAsync([conversation], proofPeopleIds, cancellationToken);
        var identity = ResolveIdentity(conversation, goal, people, meId, now);

        // The photographs are described from their own, narrower set of
        // people: the uploader and whoever confirmed, never a doubter. Handing
        // them everybody in the thread would put a doubter's name within reach
        // of a mapping that only has to slip once (ProofService.PeopleShownWith).
        var proofPeople = people
            .Where(entry => proofPeopleIds.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value);

        var photoIds = conversation.Messages.Select(m => m.ImageId).OfType<Guid>().ToList();
        var photos = photoIds.Count == 0
            ? []
            : await database.Images
                .AsNoTracking()
                .Where(image => photoIds.Contains(image.Id))
                .ToDictionaryAsync(image => image.Id, cancellationToken);

        var messages = conversation.Messages
            .OrderBy(m => m.SentAt)
            .ThenBy(m => m.Id)
            .Select(m => new ChatMessageResponse(
                m.Id,
                m.SenderPersonId,

                // Only a conversation of more than two needs to say who is
                // speaking; in a direct chat the header already does, and
                // repeating it above every bubble is noise.
                conversation.Kind != ConversationKind.Direct && m.SenderPersonId != meId
                    ? people.GetValueOrDefault(m.SenderPersonId)?.DisplayName
                    : null,
                m.Text,
                m.ImageId is { } imageId && photos.TryGetValue(imageId, out var photo)
                    ? ImageResponse.From(photo)
                    : null,
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
                    goal.ActivePauseAt(now)?.EndsOn,
                    goal.OwnerPersonId == meId),
            messages,
            conversation.IsMutedFor(meId),
            goal is null
                ? []
                : [
                    .. timeline.Select(entry => new GoalEventResponse(
                        entry.Key,
                        entry.Kind,
                        entry.At,
                        entry.ActorPersonId is { } actor && actor != meId
                            ? people.GetValueOrDefault(actor)?.DisplayName
                            : null,
                        entry.ActorPersonId == meId,
                        entry.Streak,
                        entry.ConfirmedProofs,
                        entry.RequiredProofs,
                        entry.Until,
                        entry.Proof is { } proof ? ProofService.Describe(goal, proof, meId, proofPeople, now) : null)),
                ]);
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
    /// a group, its goal's for a goal's conversation, the other person's for a
    /// direct chat.
    /// </summary>
    private static ChatIdentity ResolveIdentity(
        Conversation conversation,
        Goal? goal,
        IReadOnlyDictionary<Guid, Person> people,
        Guid meId,
        DateTimeOffset now)
    {
        if (conversation.Kind == ConversationKind.Goal)
        {
            // Drawn like a group — an icon on a neutral tile — with the goal's
            // own icon, which the client already bundles.
            return new ChatIdentity(
                goal?.Title ?? string.Empty,
                ProfileDefaults.Initials(goal?.Title),
                goal?.Icon ?? GoalIcons.Default,
                AvatarColors.Teal,
                null,
                false);
        }

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

    private static Goal? GoalOf(Conversation conversation, IReadOnlyDictionary<Guid, Goal> goals) =>
        conversation.Kind == ConversationKind.Goal && conversation.GoalId is { } goalId
            ? goals.GetValueOrDefault(goalId)
            : null;

    /// <summary>
    /// The goals of these conversations that <paramref name="meId"/> may read,
    /// with everything their timeline is made of.
    /// </summary>
    /// <remarks>
    /// Conversation membership does not grant goal access; being on the goal
    /// does. The creation path keeps the two identical, and this predicate
    /// protects against a row where they are not.
    ///
    /// Read as they are, without bringing them up to date: opening a thread
    /// should not be what decides a vote or misses a window. The maintenance
    /// pass does that within minutes, and every write through the goal or a
    /// photograph does it on the spot.
    /// </remarks>
    private async Task<Dictionary<Guid, Goal>> LoadGoalsAsync(
        IEnumerable<Conversation> conversations,
        Guid meId,
        CancellationToken cancellationToken)
    {
        var ids = conversations
            .Where(conversation => conversation.Kind == ConversationKind.Goal)
            .Select(conversation => conversation.GoalId)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        return await database.Goals
            .AsNoTracking()
            .Include(g => g.Participants)
            .Include(g => g.Instances)
            .ThenInclude(instance => instance.Proofs)
            .ThenInclude(proof => proof.Votes)
            .Include(g => g.Instances)
            .ThenInclude(instance => instance.Proofs)
            .ThenInclude(proof => proof.Reactions)
            .Include(g => g.Pauses)
            .Where(g => ids.Contains(g.Id)
                && (g.OwnerPersonId == meId || g.Participants.Any(p => p.PersonId == meId)))
            .ToDictionaryAsync(g => g.Id, cancellationToken);
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

    /// <summary>
    /// Refuses a photograph that is not the sender's own chat photograph, or
    /// that has already been sent somewhere.
    /// </summary>
    /// <remarks>
    /// Once only, because a picture's audience is the conversation its message
    /// is in. The same id in two threads would be readable from both, and
    /// deleting either conversation would take the picture out of the other.
    /// </remarks>
    private async Task EnsureSendablePhotoAsync(Guid imageId, Guid meId, CancellationToken cancellationToken)
    {
        await images.RequireOwnedAsync(imageId, meId, ImagePurpose.ChatPhoto, cancellationToken);

        if (await database.ChatMessages.AnyAsync(message => message.ImageId == imageId, cancellationToken))
        {
            throw new DomainValidationException(
                nameof(SendMessageRequest.ImageId),
                "This photograph has already been sent.");
        }
    }

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
        IEnumerable<Guid> alsoIds,
        CancellationToken cancellationToken)
    {
        var ids = conversations
            .SelectMany(c => c.Participants.Select(p => p.PersonId).Concat(c.Messages.Select(m => m.SenderPersonId)))
            .Concat(alsoIds)
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
