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

        // The open window and the one before it, for every goal at once: where
        // a row's newest line nearly always is, and what "due next" is about.
        var details = new GoalWindowDetails(database);
        await details.ReadAsync(
            goals.Values.SelectMany(goal => goal.Instances
                .OrderByDescending(window => window.DueAt)
                .ThenByDescending(window => window.Id)
                .Take(ListedWindows)),
            cancellationToken);

        // A goal's conversation is listed only while its goal can be read. The
        // two memberships are the same set of people by construction; this is
        // the guard for a row that somehow is not.
        conversations = [.. conversations.Where(conversation => conversation.Kind != ConversationKind.Goal
            || (conversation.GoalId is { } goalId && goals.ContainsKey(goalId)))];

        // The one message each row shows and a count of the rest — never the
        // rest themselves, which grow with every conversation somebody is in.
        var lastMessages = await LastMessagesAsync(conversations, cancellationToken);
        var unreadCounts = await UnreadCountsAsync(me.Id, cancellationToken);

        var people = await LoadPeopleAsync(
            conversations
                .SelectMany(conversation => conversation.Participants.Select(participant => participant.PersonId))
                .Concat(lastMessages.Values.Select(message => message.SenderPersonId)),
            cancellationToken);

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
            var last = lastMessages.GetValueOrDefault(conversation.Id);

            if (!string.IsNullOrEmpty(term)
                && !identity.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // In a goal's conversation the newest thing may be something that
            // happened to the goal — a photograph, a kept window — and that is
            // what the row shows and is sorted by, not a quieter message from
            // before it.
            //
            // Except a missed window. It is counted on the goal and stated in
            // the thread, but it is not news: a row reading "Verpasst" is the
            // first thing its owner would see of their own goal, and a miss
            // must not lift a conversation to the top of the list either
            // ("kein Nachtreten", see ActivityKind). The row falls back to what
            // came before it; the owner's row says what is due next instead.
            // A Don't's relapse and its logged uses are held back the same way:
            // owning up must not be what tops everybody's list.
            var lastEvent = goal is null ? null : await LastListedEventAsync(goal, details, now, cancellationToken);
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
                message?.IsDeleted == true,
                eventIsNewest ? lastEvent!.At : last?.SentAt,
                unreadCounts.GetValueOrDefault(conversation.Id),
                conversation.IsMutedFor(me.Id),
                conversation.IsPinnedFor(me.Id),
                eventIsNewest ? lastEvent!.Kind : null,
                goal?.Id,
                goal?.OwnerPersonId == me.Id,
                goal is not null && goal.Instances.Any(instance => waitingInWindows.Contains(instance.Id)),
                goal?.OwnerPersonId == me.Id && goal.CurrentInstance is { } open
                    ? GoalInstanceResponse.From(open)
                    : null,
                goal?.IsClosed == true));
        }

        // Pinned first (#24), then the newest conversation first, and a
        // brand-new empty one before an old silent one — which is what "sorted
        // by last activity" has to mean when there has not been any yet. The
        // app splits this list into its sections in order, so a pinned chat
        // stands at the top of its own section rather than above all of them.
        return
        [
            .. summaries
                .OrderByDescending(s => s.IsPinned)
                .ThenByDescending(s => s.LastMessageAt ?? DateTimeOffset.MinValue)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>
    /// One thread with its newest page, and marking it read.
    /// </summary>
    /// <remarks>
    /// Opening a conversation <em>is</em> reading it, so the read marker moves
    /// here rather than through a separate call the client could forget to
    /// make — leaving a badge on a chat somebody is looking at. Everything
    /// before the newest page is read on the way back up
    /// (<see cref="GetOlderAsync"/>), however long the thread has grown.
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

    /// <summary>
    /// The page of a thread before <paramref name="before"/>: a cursor that
    /// <see cref="GetAsync"/> or an earlier call to this handed out.
    /// </summary>
    /// <remarks>
    /// Not a read of the thread in the sense of the read marker: scrolling back
    /// through what was said last week says nothing about what arrived today.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">No conversation with that id exists.</exception>
    /// <exception cref="DomainValidationException">The cursor is not one this API handed out.</exception>
    public async Task<ChatPageResponse> GetOlderAsync(Guid id, string? before, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var conversation = await LoadOneAsync(id, cancellationToken);
        EnsureParticipant(conversation, me.Id);
        await EnsureVisibleAsync(conversation, me.Id, cancellationToken);

        var position = ThreadPosition.Parse(before, nameof(before));
        var goal = GoalOf(conversation, await LoadGoalsAsync([conversation], me.Id, cancellationToken));
        var page = await DescribePageAsync(conversation, goal, me.Id, now, position, cancellationToken);

        return new ChatPageResponse(page.Messages, page.Events, page.OlderCursor);
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

        MediaParts? parts = null;

        if (request.ImageId is { } imageId && imageId != Guid.Empty)
        {
            parts = await images.ResolvePartsAsync(
                imageId,
                request.PosterImageId,
                request.InsetImageId,
                request.InsetPosterImageId,
                me.Id,
                ImagePurpose.ChatPhoto,
                cancellationToken);

            await EnsureUnsentAsync([imageId, .. parts.OtherIds], cancellationToken);
        }

        var message = conversation.AddMessage(
            idGenerator.NewId(),
            me.Id,
            request.Text ?? string.Empty,
            now,
            request.ImageId,
            parts);
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

    /// <summary>
    /// Adds a reaction to a message, or takes it back, and answers with that
    /// message as it now is.
    /// </summary>
    /// <remarks>
    /// The message rather than the thread: it may be a page or two back from
    /// the newest, where an answer made of the newest page would not have it.
    /// Anything else that changed meanwhile reaches the screen over the live
    /// connection, which this touches as well.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">No such conversation or message.</exception>
    /// <exception cref="DomainValidationException">That reaction is not available.</exception>
    public async Task<ChatMessageResponse> ToggleReactionAsync(
        Guid id,
        Guid messageId,
        ToggleReactionRequest request,
        CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        var conversation = await LoadOneAsync(id, cancellationToken);
        EnsureParticipant(conversation, me.Id);
        await EnsureVisibleAsync(conversation, me.Id, cancellationToken);

        var message = await database.ChatMessages
            .Include(m => m.Reactions)
            .SingleOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversation.Id, cancellationToken)
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
                focusId: null,
                cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);

        notifier.Touch(conversation.Participants.Select(participant => participant.PersonId), LiveArea.Chats, conversation.Id);
        await notifier.FlushAsync(cancellationToken);

        var people = await LoadPeopleAsync([message.SenderPersonId], cancellationToken);
        var photos = await LoadPhotosAsync([message], cancellationToken);

        return DescribeMessage(message, conversation, me.Id, people, photos, timeProvider.GetUtcNow());
    }

    /// <summary>One message as it now is, wherever in the thread it lies.</summary>
    /// <remarks>
    /// For an open thread that was told one of its older lines changed (#25):
    /// an edit or a deletion further back than the newest page is fetched on
    /// its own rather than by reading every page loaded again.
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">No such conversation or message.</exception>
    public async Task<ChatMessageResponse> GetMessageAsync(Guid id, Guid messageId, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        var conversation = await LoadOneAsync(id, cancellationToken);
        EnsureParticipant(conversation, me.Id);
        await EnsureVisibleAsync(conversation, me.Id, cancellationToken);

        var message = await database.ChatMessages
            .AsNoTracking()
            .Include(m => m.Reactions)
            .SingleOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversation.Id, cancellationToken)
            ?? throw new ResourceNotFoundException("Message", messageId);

        return await DescribeOneAsync(message, conversation, me.Id, cancellationToken);
    }

    /// <summary>Corrects the words of your own message, within its window (#25).</summary>
    /// <exception cref="ResourceNotFoundException">No such conversation or message.</exception>
    /// <exception cref="AccessDeniedException">It is somebody else's message.</exception>
    /// <exception cref="DomainValidationException">
    /// The window has passed, it was taken back, it has no words, or the new
    /// words are empty or too long.
    /// </exception>
    public async Task<ChatMessageResponse> EditMessageAsync(
        Guid id,
        Guid messageId,
        EditMessageRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var conversation = await LoadOneAsync(id, cancellationToken);
        EnsureParticipant(conversation, me.Id);
        await EnsureVisibleAsync(conversation, me.Id, cancellationToken);

        var message = await LoadMessageAsync(conversation, messageId, cancellationToken);

        if (message.Edit(me.Id, request.Text, now))
        {
            await database.SaveChangesAsync(cancellationToken);
            await AnnounceChangedAsync(conversation, message, cancellationToken);

            // The id and nothing else — the words are the most personal thing
            // q2 stores (docs/privacy.md).
            logger.LogInformation("Message {MessageId} edited in conversation {ConversationId}", message.Id, conversation.Id);
        }

        return await DescribeOneAsync(message, conversation, me.Id, cancellationToken);
    }

    /// <summary>
    /// Takes back your own message (#25): its words, photograph and reactions
    /// go for everybody, and "Nachricht gelöscht" keeps its place.
    /// </summary>
    /// <remarks>
    /// The photograph's row goes in the same save and its bytes after it, so it
    /// leaves the sender's allowance and the disk — the order the rest of q2
    /// deletes pictures in (<see cref="ImageService.DeleteBytesAsync"/>).
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">No such conversation or message.</exception>
    /// <exception cref="AccessDeniedException">It is somebody else's message.</exception>
    public async Task<ChatMessageResponse> DeleteMessageAsync(Guid id, Guid messageId, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var conversation = await LoadOneAsync(id, cancellationToken);
        EnsureParticipant(conversation, me.Id);
        await EnsureVisibleAsync(conversation, me.Id, cancellationToken);

        var message = await LoadMessageAsync(conversation, messageId, cancellationToken);
        var wasDeleted = message.IsDeleted;
        var photos = message.Delete(me.Id, now);

        if (!wasDeleted)
        {
            var removed = await images.RemoveChatPhotosAsync(photos.Cast<Guid?>(), cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            await images.DeleteBytesAsync(removed, cancellationToken);
            await AnnounceChangedAsync(conversation, message, cancellationToken);

            logger.LogInformation("Message {MessageId} deleted in conversation {ConversationId}", message.Id, conversation.Id);
        }

        return await DescribeOneAsync(message, conversation, me.Id, cancellationToken);
    }

    private async Task<ChatMessage> LoadMessageAsync(Conversation conversation, Guid messageId, CancellationToken cancellationToken) =>
        await database.ChatMessages
            .Include(m => m.Reactions)
            .SingleOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversation.Id, cancellationToken)
        ?? throw new ResourceNotFoundException("Message", messageId);

    private async Task<ChatMessageResponse> DescribeOneAsync(
        ChatMessage message,
        Conversation conversation,
        Guid meId,
        CancellationToken cancellationToken)
    {
        var people = await LoadPeopleAsync([message.SenderPersonId], cancellationToken);
        var photos = await LoadPhotosAsync([message], cancellationToken);

        return DescribeMessage(message, conversation, meId, people, photos, timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Tells everybody in the conversation that one of its lines changed: the
    /// thread, for the newest page and the list's preview, and the line by its
    /// id, for a thread that has it further back than its newest page (#25).
    /// Nothing rings — a correction is not news.
    /// </summary>
    private async Task AnnounceChangedAsync(Conversation conversation, ChatMessage message, CancellationToken cancellationToken)
    {
        var everybody = conversation.Participants.Select(participant => participant.PersonId).ToList();

        notifier.Touch(everybody, LiveArea.Chats, conversation.Id);
        notifier.Touch(everybody, LiveArea.ChatMessage, message.Id);
        await notifier.FlushAsync(cancellationToken);
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

    /// <summary>How many conversations one person may pin (#24).</summary>
    /// <remarks>
    /// A few, because a pin is only worth something while it is rare: a list
    /// with everything pinned is the same list in a different order.
    /// </remarks>
    public const int MaxPinned = 3;

    /// <summary>Pins a conversation to the top of its section, or unpins it (#24).</summary>
    /// <exception cref="DomainValidationException">
    /// Nothing was said, or <see cref="MaxPinned"/> are pinned already.
    /// </exception>
    public async Task<ChatDetailResponse> SetPinnedAsync(Guid id, PinChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Pinned is not { } pinned)
        {
            throw new DomainValidationException(nameof(request.Pinned), "Say whether to pin the conversation.");
        }

        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var conversation = await LoadOneAsync(id, cancellationToken);
        EnsureParticipant(conversation, me.Id);
        await EnsureVisibleAsync(conversation, me.Id, cancellationToken);

        if (pinned && !conversation.IsPinnedFor(me.Id))
        {
            var alreadyPinned = await database.ConversationParticipants
                .CountAsync(participant => participant.PersonId == me.Id && participant.PinnedAt != null, cancellationToken);

            if (alreadyPinned >= MaxPinned)
            {
                throw new DomainValidationException(
                    nameof(request.Pinned),
                    $"At most {MaxPinned} conversations can be pinned.",
                    new(FieldErrorCodes.TooMany, Max: MaxPinned));
            }
        }

        conversation.SetPinned(me.Id, pinned, now);
        await database.SaveChangesAsync(cancellationToken);

        // The list on this person's other devices moves too.
        notifier.Touch([me.Id], LiveArea.Chats, conversation.Id);
        await notifier.FlushAsync(cancellationToken);

        logger.LogInformation("Conversation {ConversationId} pinned: {Pinned}", conversation.Id, pinned);

        return await DescribeAsync(conversation, me.Id, now, cancellationToken);
    }

    /// <summary>How many photographs the info sheet's grid shows at most.</summary>
    public const int MaxPhotos = 120;

    /// <summary>
    /// The photographs of a conversation, newest first (#24): those sent as
    /// messages and, in a goal's conversation, the proofs delivered against
    /// the goal.
    /// </summary>
    /// <remarks>
    /// A read of its own rather than paging through the thread (ADR 0043). A
    /// photograph whose image was deleted is left out, as on the profile's
    /// gallery, and so is a message that was taken back (#25).
    /// </remarks>
    /// <exception cref="ResourceNotFoundException">No such conversation, or not the reader's.</exception>
    public async Task<IReadOnlyList<ChatPhotoResponse>> GetPhotosAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        var conversation = await LoadOneAsync(id, cancellationToken);
        EnsureParticipant(conversation, me.Id);
        await EnsureVisibleAsync(conversation, me.Id, cancellationToken);

        var sent = await database.ChatMessages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversation.Id
                && message.ImageId != null
                && message.DeletedAt == null
                && database.Images.Any(image => image.Id == message.ImageId))
            .OrderByDescending(message => message.SentAt)
            .Take(MaxPhotos)
            .Select(message => new
            {
                message.Id,
                ImageId = message.ImageId!.Value,
                Author = message.SenderPersonId,
                At = message.SentAt,
                message.Media,
                message.PosterImageId,
                message.InsetImageId,
                message.InsetMedia,
                message.InsetPosterImageId,
            })
            .ToListAsync(cancellationToken);

        var delivered = conversation.GoalId is { } goalId
            ? await database.ProofPhotos
                .AsNoTracking()
                .Where(proof => database.GoalInstances.Any(instance => instance.Id == proof.GoalInstanceId && instance.GoalId == goalId)
                    && database.Images.Any(image => image.Id == proof.ImageId))
                .OrderByDescending(proof => proof.CreatedAt)
                .Take(MaxPhotos)
                .Select(proof => new { proof.Id, proof.ImageId, Author = proof.UploaderPersonId, At = proof.CreatedAt, proof.Source, proof.Media, proof.PosterImageId, proof.InsetImageId, proof.InsetMedia, proof.InsetPosterImageId })
                .ToListAsync(cancellationToken)
            : [];

        var people = await LoadPeopleAsync([.. sent.Select(photo => photo.Author), .. delivered.Select(photo => photo.Author)], cancellationToken);
        string NameOf(Guid person) => people.GetValueOrDefault(person)?.DisplayName ?? string.Empty;

        return
        [
            .. sent.Select(photo => new ChatPhotoResponse(
                    photo.Id,
                    photo.ImageId,
                    IsProof: false,
                    NameOf(photo.Author),
                    photo.At,
                    Source: null,
                    photo.Media,
                    photo.PosterImageId,
                    photo.InsetImageId,
                    photo.InsetMedia,
                    photo.InsetPosterImageId))
                .Concat(delivered.Select(photo => new ChatPhotoResponse(
                    photo.Id,
                    photo.ImageId,
                    IsProof: true,
                    NameOf(photo.Author),
                    photo.At,
                    photo.Source,
                    photo.Media,
                    photo.PosterImageId,
                    photo.InsetImageId,
                    photo.InsetMedia,
                    photo.InsetPosterImageId)))
                .OrderByDescending(photo => photo.At)
                .Take(MaxPhotos),
        ];
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
            // Only the photographs are needed: the messages and their
            // reactions go with the conversation's row (ConversationConfiguration).
            var photos = await database.ChatMessages
                .Where(message => message.ConversationId == conversation.Id && message.ImageId != null)
                .Select(message => new[] { message.ImageId, message.PosterImageId, message.InsetImageId, message.InsetPosterImageId })
                .ToListAsync(cancellationToken);

            removedPhotos = await images.RemoveChatPhotosAsync(photos.SelectMany(ids => ids), cancellationToken);

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
        var page = await DescribePageAsync(conversation, goal, meId, now, before: null, cancellationToken);
        var people = page.People;
        var identity = ResolveIdentity(conversation, goal, people, meId, now);
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
            other?.VisibleLastSeenAt,
            goal is null
                ? null
                : new ChatPinnedGoalResponse(
                    goal.Id,
                    goal.Title,
                    goal.Description,
                    goal.ShownInstanceAt(now) is { } window ? GoalInstanceResponse.From(window) : null,
                    goal.Streak,
                    goal.ActivePauseAt(now)?.EndsOn,
                    goal.OwnerPersonId == meId),
            page.Messages,
            conversation.IsMutedFor(meId),
            conversation.IsPinnedFor(meId),
            page.Events,

            // The reader first, then everybody else by name — the order a
            // list of people is read in everywhere else.
            [
                .. conversation.Participants
                    .Select(participant => people.GetValueOrDefault(participant.PersonId))
                    .OfType<Person>()
                    .OrderBy(person => person.Id != meId)
                    .ThenBy(person => person.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(person => PersonSummary.From(person, now)),
            ],
            page.OlderCursor);
    }

    /// <summary>
    /// One page of a thread, described for the reader: the newest without
    /// <paramref name="before"/>, the one before it with.
    /// </summary>
    /// <remarks>
    /// Reads one more message than fits and nothing else of the thread, so the
    /// cost of opening it does not grow with its length. The goal's own lines
    /// are derived from the goal: its windows whole, for the streak each kept
    /// one carries, and what hangs off them only as far back as the page
    /// reaches (<see cref="CutWithGoalAsync"/>, #127).
    /// </remarks>
    private async Task<DescribedPage> DescribePageAsync(
        Conversation conversation,
        Goal? goal,
        Guid meId,
        DateTimeOffset now,
        ThreadPosition? before,
        CancellationToken cancellationToken)
    {
        var newest = await MessagesBefore(conversation.Id, before)
            .AsNoTracking()
            .Include(m => m.Reactions)
            .Take(ThreadPage.Size + 1)
            .ToListAsync(cancellationToken);

        var page = goal is null
            ? ThreadPage.Cut(newest, [], before)
            : await CutWithGoalAsync(newest, goal, now, before, cancellationToken);
        var proofs = page.Events.Select(entry => entry.Proof).OfType<ProofPhoto>().ToList();

        var proofPeopleIds = goal is null ? [] : ProofService.PeopleShownWith(proofs, [goal]);
        var people = await LoadPeopleAsync(
            conversation.Participants
                .Select(participant => participant.PersonId)
                .Concat(page.Messages.Select(message => message.SenderPersonId))
                .Concat(proofPeopleIds)

                // An edit names whoever it added or removed, and somebody
                // removed is no longer in the thread to be found there.
                .Concat(page.Events
                    .Select(entry => entry.Change)
                    .OfType<GoalChange>()
                    .SelectMany(change => change.Checkers.Select(checker => checker.PersonId))),
            cancellationToken);

        // The photographs are described from their own, narrower set of
        // people: the uploader and whoever confirmed, never a doubter. Handing
        // them everybody in the thread would put a doubter's name within reach
        // of a mapping that only has to slip once (ProofService.PeopleShownWith).
        var proofPeople = people
            .Where(entry => proofPeopleIds.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value);

        var photos = await LoadPhotosAsync(page.Messages, cancellationToken);

        return new DescribedPage(
            [.. page.Messages.Select(message => DescribeMessage(message, conversation, meId, people, photos, now))],
            goal is null
                ? []
                : [
                    .. page.Events.Select(entry => new GoalEventResponse(
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
                        entry.Proof is { } proof ? ProofService.Describe(goal, proof, meId, proofPeople, now) : null,
                        entry.Day,
                        entry.Uses,
                        entry.Limit,
                        entry.Use?.ImageId,
                        GoalChangeResponse.From(entry.Change, goal, people, meId, now),
                        entry.Use?.ImageId is null ? null : entry.Use.Media,
                        entry.Use?.PosterImageId,
                        entry.Use?.InsetImageId,
                        entry.Use?.InsetMedia,
                        entry.Use?.InsetPosterImageId)),
                ],
            page.Older?.ToCursor(),
            people);
    }

    /// <summary>A page as the reader is shown it, with the people it was described from.</summary>
    private sealed record DescribedPage(
        IReadOnlyList<ChatMessageResponse> Messages,
        IReadOnlyList<GoalEventResponse> Events,
        string? OlderCursor,
        IReadOnlyDictionary<Guid, Person> People);

    /// <summary>
    /// A conversation's messages before <paramref name="before"/>, newest
    /// first — the order <see cref="ThreadPosition"/> puts them in, in SQL.
    /// </summary>
    private IQueryable<ChatMessage> MessagesBefore(Guid conversationId, ThreadPosition? before)
    {
        var messages = database.ChatMessages.Where(message => message.ConversationId == conversationId);

        if (before is { } end)
        {
            var at = end.At;
            var id = end.Id;

            // A line about the goal sorts before every message at its instant,
            // so only a message's place splits an instant by id.
            messages = end.IsMessage
                ? messages.Where(message => message.SentAt < at || (message.SentAt == at && message.Id < id))
                : messages.Where(message => message.SentAt < at);
        }

        return messages
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id);
    }

    private static ChatMessageResponse DescribeMessage(
        ChatMessage message,
        Conversation conversation,
        Guid meId,
        IReadOnlyDictionary<Guid, Person> people,
        IReadOnlyDictionary<Guid, StoredImage> photos,
        DateTimeOffset now) =>
        new(
            message.Id,
            message.SenderPersonId,

            // Only a conversation of more than two needs to say who is
            // speaking; in a direct chat the header already does, and
            // repeating it above every bubble is noise.
            conversation.Kind != ConversationKind.Direct && message.SenderPersonId != meId
                ? people.GetValueOrDefault(message.SenderPersonId)?.DisplayName
                : null,
            message.Text,
            message.ImageId is { } imageId && photos.TryGetValue(imageId, out var photo)
                ? ImageResponse.From(photo)
                : null,
            message.SenderPersonId == meId,
            message.SentAt,
            SummariseReactions(message, meId),
            message.EditedAt,
            message.IsDeleted,
            message.CanBeEditedBy(meId, now),
            message.PosterImageId,
            message.InsetImageId,
            message.InsetMedia,
            message.InsetPosterImageId);

    private async Task<IReadOnlyDictionary<Guid, StoredImage>> LoadPhotosAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        var photoIds = messages.Select(m => m.ImageId).OfType<Guid>().ToList();

        return photoIds.Count == 0
            ? new Dictionary<Guid, StoredImage>()
            : await database.Images
                .AsNoTracking()
                .Where(image => photoIds.Contains(image.Id))
                .ToDictionaryAsync(image => image.Id, cancellationToken);
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
                goal?.Icon ?? GoalCategories.IconFor(GoalCategories.Default),
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

    /// <summary>How many of a goal's newest windows the list reads the details of up front.</summary>
    private const int ListedWindows = 2;

    /// <summary>
    /// The newest line of a goal's timeline a row shows: anything but a miss,
    /// a relapse or a logged use (see the list).
    /// </summary>
    /// <remarks>
    /// Exact once the line found is later than anything an unread window could
    /// still add (<see cref="GoalWindowDetails.Horizon"/>); until then the
    /// next, twice as many windows are read. A goal whose newest windows were
    /// all missed is the one that reads further back.
    /// </remarks>
    private static async Task<GoalEvent?> LastListedEventAsync(
        Goal goal,
        GoalWindowDetails details,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        for (var count = ListedWindows; ; count *= 2)
        {
            var last = GoalTimeline.For(goal, now)
                .LastOrDefault(entry => entry.Kind is not (GoalEventKind.WindowMissed or GoalEventKind.WindowRelapse or GoalEventKind.UseLogged));

            if (details.Horizon(goal, by: null) is not { } horizon || (last is not null && last.At > horizon))
            {
                return last;
            }

            await details.ReadNewestAsync(goal, count, by: null, cancellationToken);
        }
    }

    /// <summary>
    /// Cuts a page of a goal's conversation, reading the details of only as
    /// many of its windows as the page reaches back to (#127).
    /// </summary>
    /// <remarks>
    /// Starts with the window shown on the banner and the newest windows that
    /// began before the page ends — one more than a page of lines, since every
    /// window that ended adds a line. The page is exact when it does not begin
    /// the thread and its oldest line is later than anything an unread window
    /// could add; otherwise twice as many windows are read and it is cut again.
    /// </remarks>
    private async Task<ThreadPage> CutWithGoalAsync(
        IReadOnlyList<ChatMessage> newest,
        Goal goal,
        DateTimeOffset now,
        ThreadPosition? before,
        CancellationToken cancellationToken)
    {
        var details = new GoalWindowDetails(database);
        var by = before?.At;

        if (goal.ShownInstanceAt(now) is { } shown)
        {
            await details.ReadAsync([shown], cancellationToken);
        }

        for (var count = ThreadPage.Size + 1; ; count *= 2)
        {
            await details.ReadNewestAsync(goal, count, by, cancellationToken);

            var page = ThreadPage.Cut(newest, GoalTimeline.For(goal, now), before);

            if (details.Horizon(goal, by) is not { } horizon || (page.Older is { } oldest && oldest.At > horizon))
            {
                return page;
            }
        }
    }

    private static Goal? GoalOf(Conversation conversation, IReadOnlyDictionary<Guid, Goal> goals) =>
        conversation.Kind == ConversationKind.Goal && conversation.GoalId is { } goalId
            ? goals.GetValueOrDefault(goalId)
            : null;

    /// <summary>
    /// The goals of these conversations that <paramref name="meId"/> may read:
    /// each with its windows, pauses and edits, but not yet with what hangs off
    /// the windows — <see cref="GoalWindowDetails"/> reads that for the windows
    /// a page or a row needs (#127).
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

        // Tracked, unlike most reads here: the windows' details are read
        // afterwards, and only a tracked window takes them in. Nothing read
        // here is changed, so nothing of it is ever saved.
        return await database.Goals
            .Include(g => g.Participants)
            .Include(g => g.Instances)
            .Include(g => g.Pauses)
            .Include(g => g.Changes)
            .ThenInclude(change => change.Checkers)
            .Include(g => g.Changes)
            .ThenInclude(change => change.Vetoes)
            .Where(g => ids.Contains(g.Id)
                && (g.OwnerPersonId == meId || g.Participants.Any(p => p.PersonId == meId)))
            .ToDictionaryAsync(g => g.Id, cancellationToken);
    }

    /// <remarks>
    /// The participants only. What each row shows of the messages is read by
    /// <see cref="LastMessagesAsync"/> and <see cref="UnreadCountsAsync"/>.
    /// </remarks>
    private async Task<List<Conversation>> LoadMineAsync(Guid meId, CancellationToken cancellationToken) =>
        await database.Conversations
            .AsNoTracking()
            .Include(c => c.Participants)
            .Where(c => c.Participants.Any(p => p.PersonId == meId))
            .ToListAsync(cancellationToken);

    /// <summary>The newest message of each of these conversations that has one.</summary>
    /// <remarks>
    /// Picked in the query, one row per conversation, in the order the thread
    /// itself is read in: <c>(SentAt, Id)</c>.
    /// </remarks>
    private async Task<Dictionary<Guid, ChatMessage>> LastMessagesAsync(
        IReadOnlyCollection<Conversation> conversations,
        CancellationToken cancellationToken)
    {
        var ids = conversations.Select(conversation => conversation.Id).ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        return await database.ChatMessages
            .AsNoTracking()
            .Where(message => ids.Contains(message.ConversationId))
            .GroupBy(message => message.ConversationId)
            .Select(thread => thread
                .OrderByDescending(message => message.SentAt)
                .ThenByDescending(message => message.Id)
                .First())
            .ToDictionaryAsync(message => message.ConversationId, cancellationToken);
    }

    /// <summary>
    /// How many messages from other people this person has not seen, per
    /// conversation — counted after their read marker, so the number can never
    /// drift from the messages themselves.
    /// </summary>
    /// <remarks>
    /// Counted by the database. Only conversations with something unread have
    /// a row; the rest are zero.
    /// </remarks>
    private async Task<Dictionary<Guid, int>> UnreadCountsAsync(Guid meId, CancellationToken cancellationToken) =>
        await database.ChatMessages
            .AsNoTracking()
            .Join(
                database.ConversationParticipants.Where(participant => participant.PersonId == meId),
                message => message.ConversationId,
                reader => reader.ConversationId,
                (message, reader) => new { message.ConversationId, message.SenderPersonId, message.SentAt, reader.LastReadAt })
            .Where(row => row.SenderPersonId != meId && (row.LastReadAt == null || row.SentAt > row.LastReadAt))
            .GroupBy(row => row.ConversationId)
            .Select(thread => new { ConversationId = thread.Key, Count = thread.Count() })
            .ToDictionaryAsync(row => row.ConversationId, row => row.Count, cancellationToken);

    /// <remarks>
    /// The participants and nothing of the thread: a page of it is read by
    /// <see cref="DescribePageAsync"/>, and the one message a reaction is about
    /// by itself.
    /// </remarks>
    private async Task<Conversation> LoadOneAsync(Guid id, CancellationToken cancellationToken) =>
        await database.Conversations
            .Include(c => c.Participants)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
        ?? throw new ResourceNotFoundException("Conversation", id);

    /// <summary>
    /// Refuses a picture — or any part of one — that has already been sent
    /// somewhere. That each is the sender's own chat photograph is
    /// <see cref="ImageService.ResolvePartsAsync"/>' check.
    /// </summary>
    /// <remarks>
    /// Once only, because a picture's audience is the conversation its message
    /// is in. The same id in two threads would be readable from both, and
    /// deleting either conversation would take the picture out of the other.
    /// </remarks>
    private async Task EnsureUnsentAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var sent = await database.ChatMessages.AnyAsync(
            message => (message.ImageId != null && ids.Contains(message.ImageId.Value))
                || (message.PosterImageId != null && ids.Contains(message.PosterImageId.Value))
                || (message.InsetImageId != null && ids.Contains(message.InsetImageId.Value))
                || (message.InsetPosterImageId != null && ids.Contains(message.InsetPosterImageId.Value)),
            cancellationToken);

        if (sent)
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
        IEnumerable<Guid> personIds,
        CancellationToken cancellationToken)
    {
        var ids = personIds.Distinct().ToList();

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
