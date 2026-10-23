using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Activity;
using Q2.Api.Features.Challenges;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Images;
using Q2.Api.Features.Moderation;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;
using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Accounts;

/// <summary>
/// Everything q2 holds about the signed-in person, as one ZIP file: their data
/// as JSON and the pictures that are theirs (#36, Art. 20 GDPR).
/// </summary>
/// <remarks>
/// **The person's share, field by field.** Each section names the fields it
/// writes rather than serialising rows, so a credential — the password hash, a
/// security stamp, a device's push keys — cannot slip into an export because a
/// column was added. Which tables there are comes from
/// <see cref="PersonalData"/>, which a test holds against the model.
///
/// Other people appear by id, and once each in <c>people</c> with the name and
/// handle the app shows anyway. Their messages, photographs and votes are not
/// the person's and are not here: on the person's own photographs only how many
/// believed and doubted, and why — counted, as the app counts them
/// (docs/adr/0055-why-a-photograph-was-doubted.md).
///
/// Written to a temporary file and streamed from there: a ZIP is written
/// synchronously, which a response body does not allow, and up to the whole
/// image allowance does not belong in memory. The file deletes itself when the
/// response is done with it.
/// </remarks>
public sealed class DataExportService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    IImageStore imageStore,
    TimeProvider timeProvider,
    ILogger<DataExportService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The export, ready to send, and the name to save it under.</summary>
    public async Task<(Stream Archive, string FileName)> ExportAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var data = await CollectAsync(me, now, cancellationToken);

        var path = Path.GetTempFileName();
        var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);

        try
        {
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zip.CreateEntry("data.json", CompressionLevel.Optimal);
                await using (var json = entry.Open())
                {
                    await JsonSerializer.SerializeAsync(json, data.Document, Json, cancellationToken);
                }

                foreach (var image in data.Images)
                {
                    await using var bytes = await imageStore.OpenAsync(image.Id, cancellationToken);

                    // A row whose file is gone is listed and not packed: the
                    // export says what q2 holds, and it holds no bytes for it.
                    if (bytes is null)
                    {
                        continue;
                    }

                    // Pictures are compressed already.
                    await using var target = zip.CreateEntry(FileOf(image), CompressionLevel.NoCompression).Open();
                    await bytes.CopyToAsync(target, cancellationToken);
                }
            }

            file.Position = 0;
        }
        catch
        {
            await file.DisposeAsync();
            throw;
        }

        logger.LogInformation("Data exported for {PersonId}: {ImageCount} image(s)", me.Id, data.Images.Count);

        return (file, $"qdos-{DateOnly.FromDateTime(now.UtcDateTime):yyyy-MM-dd}.zip");
    }

    private sealed record Collected(Dictionary<string, object?> Document, IReadOnlyList<StoredImage> Images);

    private static string FileOf(StoredImage image) =>
        $"images/{image.Id:N}{image.ContentType switch
        {
            ImageFormatReader.Png => ".png",
            ImageFormatReader.Mp4 => ".mp4",
            _ => ".jpg",
        }}";

    private async Task<Collected> CollectAsync(Person me, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var id = me.Id;
        var people = new HashSet<Guid>();

        var images = await database.Images.AsNoTracking()
            .Where(image => image.OwnerPersonId == id)
            .OrderBy(image => image.CreatedAt)
            .ToListAsync(cancellationToken);
        var files = images.ToDictionary(image => image.Id, FileOf);
        string? FileFor(Guid? imageId) => imageId is { } known ? files.GetValueOrDefault(known) : null;

        var account = await database.Users.AsNoTracking()
            .Where(user => user.PersonId == id)
            .Select(user => new { user.Email, user.EmailConfirmed, user.TwoFactorEnabled })
            .SingleOrDefaultAsync(cancellationToken);

        var settings = await database.UserSettings.AsNoTracking()
            .SingleOrDefaultAsync(row => row.PersonId == id, cancellationToken);

        var checkIns = await database.DailyCheckIns.AsNoTracking()
            .Where(day => day.PersonId == id)
            .OrderBy(day => day.Date)
            .Select(day => day.Date)
            .ToListAsync(cancellationToken);

        var badges = await database.PersonBadges.AsNoTracking()
            .Where(badge => badge.PersonId == id)
            .OrderBy(badge => badge.EarnedOn)
            .Select(badge => new { badge.Badge, badge.EarnedOn })
            .ToListAsync(cancellationToken);

        var friendships = (await database.Friendships.AsNoTracking()
                .Where(f => f.RequesterId == id || f.AddresseeId == id)
                .OrderBy(f => f.RequestedAt)
                .ToListAsync(cancellationToken))
            .Select(f =>
            {
                var other = f.RequesterId == id ? f.AddresseeId : f.RequesterId;
                people.Add(other);
                return new { With = other, Sent = f.RequesterId == id, f.Status, f.RequestedAt, f.RespondedAt };
            })
            .ToList();

        var blocks = await database.Blocks.AsNoTracking()
            .Where(block => block.BlockerPersonId == id)
            .OrderBy(block => block.CreatedAt)
            .Select(block => new { Blocked = block.BlockedPersonId, At = block.CreatedAt })
            .ToListAsync(cancellationToken);
        people.UnionWith(blocks.Select(block => block.Blocked));

        var goals = await GoalsAsync(id, people, FileFor, cancellationToken);
        var checking = await CheckingAsync(id, people, cancellationToken);

        var objections = new
        {
            Pauses = await database.PauseVetoes.AsNoTracking()
                .Where(veto => veto.PersonId == id)
                .Select(veto => new { Pause = veto.GoalPauseId, At = veto.CreatedAt })
                .ToListAsync(cancellationToken),
            Changes = await database.GoalChangeVetoes.AsNoTracking()
                .Where(veto => veto.PersonId == id)
                .Select(veto => new { Change = veto.GoalChangeId, At = veto.CreatedAt })
                .ToListAsync(cancellationToken),
        };

        var drafts = await database.GoalDrafts.AsNoTracking()
            .Where(draft => draft.OwnerPersonId == id)
            .Select(draft => new { draft.Title, draft.Category, draft.ReminderAt, draft.CreatedAt })
            .ToListAsync(cancellationToken);

        var votes = await database.ProofVotes.AsNoTracking()
            .Where(vote => vote.VoterPersonId == id)
            .OrderBy(vote => vote.CastAt)
            .Select(vote => new
            {
                Photograph = vote.ProofPhotoId,
                Goal = database.GoalInstances
                    .Where(window => database.ProofPhotos.Any(proof => proof.Id == vote.ProofPhotoId && proof.GoalInstanceId == window.Id))
                    .Select(window => window.GoalId)
                    .FirstOrDefault(),
                vote.Value,
                vote.Reason,
                vote.CastAt,
            })
            .ToListAsync(cancellationToken);

        var reactions = new
        {
            Photographs = await database.ProofReactions.AsNoTracking()
                .Where(reaction => reaction.PersonId == id)
                .Select(reaction => new { Photograph = reaction.ProofPhotoId, reaction.Kind })
                .ToListAsync(cancellationToken),
            ChallengeEntries = await database.ChallengeReactions.AsNoTracking()
                .Where(reaction => reaction.PersonId == id)
                .Select(reaction => new { Entry = reaction.ChallengeEntryId, reaction.Kind })
                .ToListAsync(cancellationToken),
            Messages = await database.MessageReactions.AsNoTracking()
                .Where(reaction => reaction.PersonId == id)
                .Select(reaction => new { Message = reaction.MessageId, reaction.Kind })
                .ToListAsync(cancellationToken),
        };

        var challengeEntries = (await database.ChallengeEntries.AsNoTracking()
                .Where(entry => entry.PersonId == id)
                .Join(database.Challenges, entry => entry.ChallengeId, challenge => challenge.Id, (entry, challenge) => new { entry, challenge })
                .OrderBy(row => row.challenge.Day)
                .ToListAsync(cancellationToken))
            .Select(row => new
            {
                row.challenge.Day,
                row.challenge.Prompt,
                row.challenge.PromptEnglish,
                row.entry.CreatedAt,
                row.entry.Source,
                Late = row.challenge.IsLate(row.entry.CreatedAt),
                row.entry.Media,
                Image = FileFor(row.entry.ImageId),
                Still = FileFor(row.entry.PosterImageId),
                FrontCamera = FileFor(row.entry.InsetImageId),
                FrontCameraMedia = row.entry.InsetMedia,
                FrontCameraStill = FileFor(row.entry.InsetPosterImageId),
            })
            .ToList();

        var activity = await database.ActivityEvents.AsNoTracking()
            .Where(line => line.ActorPersonId == id)
            .OrderBy(line => line.OccurredAt)
            .Select(line => new { line.Kind, line.Subject, line.Amount, line.OccurredAt, line.KudosCount })
            .ToListAsync(cancellationToken);

        var kudosGiven = await database.ActivityKudos.AsNoTracking()
            .Where(kudos => kudos.PersonId == id)
            .Join(database.ActivityEvents, kudos => kudos.ActivityEventId, line => line.Id, (kudos, line) => new
            {
                Line = line.Id,
                To = line.ActorPersonId,
                line.Kind,
                line.OccurredAt,
            })
            .ToListAsync(cancellationToken);
        people.UnionWith(kudosGiven.Select(kudos => kudos.To));

        var conversations = await database.ConversationParticipants.AsNoTracking()
            .Where(participant => participant.PersonId == id)
            .Join(database.Conversations, participant => participant.ConversationId, conversation => conversation.Id, (participant, conversation) => new
            {
                conversation.Id,
                conversation.Kind,
                conversation.Title,
                conversation.GoalId,
                participant.LastReadAt,
                participant.MutedAt,
                participant.PinnedAt,
                With = database.ConversationParticipants
                    .Where(other => other.ConversationId == conversation.Id && other.PersonId != id)
                    .Select(other => other.PersonId)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);
        people.UnionWith(conversations.SelectMany(conversation => conversation.With));

        var messages = (await database.ChatMessages.AsNoTracking()
                .Where(message => message.SenderPersonId == id)
                .OrderBy(message => message.SentAt)
                .ToListAsync(cancellationToken))
            .Select(message => new
            {
                Conversation = message.ConversationId,
                message.SentAt,
                message.Text,
                message.EditedAt,
                message.DeletedAt,
                Media = message.ImageId is null ? (MediaKind?)null : message.Media,
                Image = FileFor(message.ImageId),
                Still = FileFor(message.PosterImageId),
                FrontCamera = FileFor(message.InsetImageId),
                FrontCameraMedia = message.InsetMedia,
                FrontCameraStill = FileFor(message.InsetPosterImageId),
            })
            .ToList();

        var notifications = await database.Notifications.AsNoTracking()
            .Where(line => line.RecipientPersonId == id)
            .OrderBy(line => line.OccurredAt)
            .Select(line => new
            {
                line.Kind,
                From = line.ActorPersonId,
                line.Subject,
                line.Amount,
                line.Detail,
                line.Target,
                line.TargetId,
                line.OccurredAt,
            })
            .ToListAsync(cancellationToken);
        people.UnionWith(notifications.Select(line => line.From).OfType<Guid>());

        // Where a device's pushes go, not how they are encrypted: the keys are
        // a credential of the device, and the address a capability.
        var devices = (await database.PushSubscriptions.AsNoTracking()
                .Where(device => device.PersonId == id)
                .Select(device => new { device.Endpoint, device.CreatedAt, device.LastDeliveredAt })
                .ToListAsync(cancellationToken))
            .Select(device => new
            {
                Service = Uri.TryCreate(device.Endpoint, UriKind.Absolute, out var endpoint) ? endpoint.Host : null,
                device.CreatedAt,
                device.LastDeliveredAt,
            })
            .ToList();

        var reports = await database.Reports.AsNoTracking()
            .Where(report => report.ReporterPersonId == id)
            .OrderBy(report => report.CreatedAt)
            .Select(report => new { report.TargetKind, report.TargetId, report.Reason, report.Note, report.CreatedAt })
            .ToListAsync(cancellationToken);

        people.Remove(id);
        var named = await database.People.AsNoTracking()
            .Where(person => people.Contains(person.Id))
            .OrderBy(person => person.DisplayName)
            .Select(person => new { person.Id, person.DisplayName, person.Handle })
            .ToListAsync(cancellationToken);

        var document = new Dictionary<string, object?>
        {
            ["exportedAt"] = now,
            ["person"] = new
            {
                me.Id,
                me.DisplayName,
                me.Handle,
                me.Initials,
                me.AvatarColor,
                Avatar = FileFor(me.AvatarImageId),
                me.Bio,
                TimeZone = me.TimeZoneId,
                me.Findability,
                me.ShowsActivity,
                me.InviteCode,
                me.KudosReceived,
                me.GoalsCompleted,
                me.OnboardedAt,
                me.LastSeenAt,
                me.NotificationsSeenAt,
            },
            ["account"] = account,
            ["settings"] = settings is null
                ? null
                : new { settings.Theme, settings.Language, Notifications = NotificationSettingsResponse.From(settings.Notifications) },
            ["checkIns"] = checkIns,
            ["badges"] = badges,
            ["friendships"] = friendships,
            ["blocks"] = blocks,
            ["goals"] = goals,
            ["checking"] = checking,
            ["objections"] = objections,
            ["drafts"] = drafts,
            ["votes"] = votes,
            ["reactions"] = reactions,
            ["images"] = images.Select(image => new
            {
                image.Id,
                image.Purpose,
                image.ContentType,
                image.Width,
                image.Height,
                image.ByteSize,
                image.CreatedAt,
                File = FileOf(image),
            }),
            ["challengeEntries"] = challengeEntries,
            ["activity"] = activity,
            ["kudosGiven"] = kudosGiven,
            ["conversations"] = conversations,
            ["messages"] = messages,
            ["notifications"] = notifications,
            ["devices"] = devices,
            ["reports"] = reports,
            ["people"] = named,
        };

        return new Collected(document, images);
    }

    /// <summary>The person's own goals, each with its windows, photographs, pauses and edits.</summary>
    private async Task<object> GoalsAsync(Guid id, HashSet<Guid> people, Func<Guid?, string?> fileFor, CancellationToken cancellationToken)
    {
        var goals = await database.Goals.AsNoTracking()
            .Include(goal => goal.Participants)
            .Include(goal => goal.Instances).ThenInclude(window => window.Proofs).ThenInclude(proof => proof.Votes)
            .Include(goal => goal.Instances).ThenInclude(window => window.UseLog)
            .Include(goal => goal.Instances).ThenInclude(window => window.SpotChecks)
            .Include(goal => goal.Pauses)
            .Include(goal => goal.Changes).ThenInclude(change => change.Checkers)
            .Where(goal => goal.OwnerPersonId == id)
            .OrderBy(goal => goal.CreatedAt)
            .ToListAsync(cancellationToken);

        people.UnionWith(goals.SelectMany(goal => goal.Participants.Select(participant => participant.PersonId)));
        people.UnionWith(goals.SelectMany(goal => goal.Changes.SelectMany(change => change.Checkers.Select(checker => checker.PersonId))));

        return goals.Select(goal => new
        {
            goal.Id,
            goal.Title,
            goal.Description,
            goal.Category,
            goal.Kind,
            Schedule = GoalScheduleResponse.From(goal.Schedule),
            goal.Limit,
            goal.ProofMode,
            goal.ReminderAt,
            goal.TargetDate,
            goal.StartsOn,
            goal.IsGroup,
            goal.Status,
            goal.CreatedAt,
            goal.ClosedAt,
            Checkers = goal.Participants.Select(participant => participant.PersonId),
            Windows = goal.Instances.OrderBy(window => window.DueAt).Select(window => new
            {
                window.StartsOn,
                window.DueOn,
                window.Status,
                window.RequiredProofs,
                window.ConfirmedProofs,
                window.Limit,
                window.ResolvedAt,
                Photographs = window.Proofs.OrderBy(proof => proof.CreatedAt).Select(proof => new
                {
                    proof.Id,
                    proof.CreatedAt,
                    proof.Source,
                    proof.Media,
                    proof.Attempt,
                    proof.Status,
                    proof.ResolvedAt,
                    Image = fileFor(proof.ImageId),
                    Still = fileFor(proof.PosterImageId),
                    FrontCamera = fileFor(proof.InsetImageId),
                    FrontCameraMedia = proof.InsetMedia,
                    FrontCameraStill = fileFor(proof.InsetPosterImageId),

                    // Counted, never who: as the owner's own screen shows it.
                    Believed = proof.Votes.Count(vote => vote.Value == VoteValue.Confirm),
                    Doubted = proof.Votes.Count(vote => vote.Value == VoteValue.Doubt),
                    DoubtReasons = proof.DoubtReasons.Select(reason => new { reason.Reason, reason.Count }),
                }),
                Uses = window.UseLog.OrderBy(use => use.LoggedAt).Select(use => new
                {
                    use.LoggedAt,
                    Media = use.ImageId is null ? (MediaKind?)null : use.Media,
                    Image = fileFor(use.ImageId),
                    Still = fileFor(use.PosterImageId),
                    FrontCamera = fileFor(use.InsetImageId),
                    FrontCameraMedia = use.InsetMedia,
                    FrontCameraStill = fileFor(use.InsetPosterImageId),
                }),
                SpotChecks = window.SpotChecks.OrderBy(check => check.Day).Select(check => new { check.Day, check.Status, check.IssuedAt, check.ExpiresAt }),
            }),
            Pauses = goal.Pauses.OrderBy(pause => pause.CreatedAt).Select(pause => new
            {
                pause.Reason,
                pause.StartsOn,
                pause.EndsOn,
                pause.Status,
                pause.CreatedAt,
            }),
            Changes = goal.Changes.OrderBy(change => change.CreatedAt).Select(change => new
            {
                change.CreatedAt,
                change.Status,
                change.AppliesOn,
                change.ResolvedAt,
                change.TitleFrom,
                change.TitleTo,
                change.DescriptionFrom,
                change.DescriptionTo,
                change.CategoryFrom,
                change.CategoryTo,
                change.LimitFrom,
                change.LimitTo,
                change.ProofModeFrom,
                change.ProofModeTo,
                change.TargetDateFrom,
                change.TargetDateTo,
                Checkers = change.Checkers.Select(checker => new { Person = checker.PersonId, checker.Added }),
            }),
        }).ToList();
    }

    /// <summary>The goals the person checks for somebody else, and the edits they were named in.</summary>
    private async Task<object> CheckingAsync(Guid id, HashSet<Guid> people, CancellationToken cancellationToken)
    {
        var goals = await database.GoalParticipants.AsNoTracking()
            .Where(participant => participant.PersonId == id)
            .Join(database.Goals, participant => participant.GoalId, goal => goal.Id, (participant, goal) => new
            {
                Goal = goal.Id,
                goal.Title,
                Owner = goal.OwnerPersonId,
            })
            .ToListAsync(cancellationToken);
        people.UnionWith(goals.Select(goal => goal.Owner));

        var namedInEdits = await database.GoalChangeCheckers.AsNoTracking()
            .Where(checker => checker.PersonId == id)
            .Join(database.GoalChanges, checker => checker.GoalChangeId, change => change.Id, (checker, change) => new
            {
                Change = change.Id,
                Goal = change.GoalId,
                checker.Added,
                change.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return new { Goals = goals, NamedInEdits = namedInEdits };
    }
}
