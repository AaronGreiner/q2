# Architecture decision records

One file per decision that would otherwise be re-litigated every few months.

An ADR records *why*, not *how* — the how is in the code. If a decision here is
reversed, add a new record that supersedes the old one rather than editing it,
so the reasoning at the time stays readable.

| # | Decision | Status |
| --- | --- | --- |
| [0001](0001-repository-layout-and-task-runner.md) | One repository, two applications, one task runner | Accepted |
| [0002](0002-frontend-architecture.md) | Nuxt with a component architecture on top of Nuxt UI | Accepted |
| [0003](0003-backend-architecture.md) | Feature-organised minimal APIs, no repository layer | Accepted |
| [0004](0004-sqlite-first.md) | SQLite first, PostgreSQL-ready | Accepted |
| [0005](0005-observability-and-sentry.md) | Sentry in every environment, filtered centrally | Accepted |
| [0006](0006-authentication-deferred.md) | Authentication deliberately deferred | Superseded by [0011](0011-authentication-with-identity.md) |
| [0007](0007-testing-strategy.md) | Real providers, real pipeline, real SDK | Accepted |
| [0008](0008-deployment-topology.md) | One host, two systemd services, one origin | Accepted |
| [0009](0009-single-known-person.md) | One known person, flagged in the database | Superseded by [0011](0011-authentication-with-identity.md) |
| [0010](0010-german-first-interface.md) | A German-first interface, with a hand-written catalogue | Accepted |
| [0011](0011-authentication-with-identity.md) | Accounts with ASP.NET Core Identity and a session cookie | Accepted, amended by [0026](0026-mail-and-password-reset.md) and [0034](0034-bearer-tokens-for-the-native-app.md) |
| [0012](0012-installable-pwa.md) | Installable as a PWA, with a service worker that caches no content | Accepted |
| [0013](0013-app-like-input.md) | App-like input: no zoom, no selection, a capped safe area | Accepted |
| [0014](0014-user-feedback.md) | User feedback: anonymous, from our own controls | Accepted |
| [0015](0015-qdos-design-language.md) | The Qdos design language: black, one accent, no emoji | Amended by [0028](0028-ruhe-design-system.md), [0031](0031-todos-tab-and-a-bell-you-can-empty.md), [0047](0047-one-name-on-screen.md) and [0050](0050-categories-and-badges-that-are-earned.md) |
| [0016](0016-windows-instead-of-steps.md) | Windows instead of steps: a goal you can miss | Accepted, amended by [0037](0037-donts.md), [0049](0049-streak-milestones-in-the-feed.md), [0051](0051-a-first-window-that-can-be-met.md), [0054](0054-the-zone-your-days-are-counted-in.md) and [0059](0059-a-do-that-ends-at-a-time-of-day.md) |
| [0017](0017-image-storage.md) | Images: bytes on disk, behind a session | Accepted |
| [0018](0018-proof-and-vote.md) | A window is closed by other people | Accepted, amended by [0027](0027-goal-conversations.md), [0030](0030-swipe-vote-and-full-screen-photos.md), [0036](0036-live-or-screenshot.md), [0037](0037-donts.md) and [0055](0055-why-a-photograph-was-doubted.md) |
| [0019](0019-warning-and-balance.md) | The warning before, and the record after | Accepted, amended by [0024](0024-one-notification-pipeline.md), [0037](0037-donts.md) and [0059](0059-a-do-that-ends-at-a-time-of-day.md) |
| [0020](0020-pause-and-archive.md) | The exits: a pause, an ending, and a deletion | Accepted, amended by [0027](0027-goal-conversations.md) and [0052](0052-pauses-planned-ahead-and-for-everything.md); a one-off after a pause by [#129](https://github.com/AaronGreiner/q2/issues/129) |
| [0021](0021-daily-challenge.md) | The daily challenge: one prompt, and nothing at stake | Accepted, amended by [0045](0045-what-waits-for-you-first.md) and [0056](0056-the-challenge-has-a-moment.md) |
| [0022](0022-blocking-reporting-and-erasure.md) | Getting away, asking for help, and leaving | Accepted, amended by [0033](0033-invite-links-for-new-and-existing-accounts.md) and [0058](0058-your-data-as-one-file.md) |
| [0023](0023-web-push.md) | Notifications: a delivery route, not a second product | Accepted, amended by [0024](0024-one-notification-pipeline.md) and [0056](0056-the-challenge-has-a-moment.md) |
| [0024](0024-one-notification-pipeline.md) | One notification pipeline: the bell, a live connection, and push | Accepted, amended by [0025](0025-banners-in-the-open-app.md), [0031](0031-todos-tab-and-a-bell-you-can-empty.md), [0038](0038-spot-checks.md) and [0048](0048-the-daily-reminder-is-sent.md) |
| [0025](0025-banners-in-the-open-app.md) | Banners in the open app: the push for somebody who is looking | Accepted |
| [0026](0026-mail-and-password-reset.md) | Mail, and the way back into an account | Accepted |
| [0027](0027-goal-conversations.md) | Every goal is checked in its own conversation | Accepted, amended by [0032](0032-the-next-step-first.md), [0039](0039-first-run-onboarding.md), [0043](0043-threads-a-page-at-a-time.md) and [0044](0044-editing-a-running-goal.md) |
| [0028](0028-ruhe-design-system.md) | Ruhe: a continuous surface and selectable accents | Accepted |
| [0029](0029-photographs-in-conversations.md) | Photographs in conversations, and no ready-made replies | Accepted, amended by [0046](0046-looking-after-a-conversation.md) |
| [0030](0030-swipe-vote-and-full-screen-photos.md) | A verdict by swipe, a vote you can take back, and every photograph full size | Accepted, amended by [0036](0036-live-or-screenshot.md) |
| [0031](0031-todos-tab-and-a-bell-you-can-empty.md) | A To-Dos tab, a shorter start screen, and a bell you can empty | Accepted, amended by [0032](0032-the-next-step-first.md) and [0035](0035-dos-and-donts.md) |
| [0032](0032-the-next-step-first.md) | The next step first: urgency, folded misses, and a goal made in steps | Accepted, amended by [0041](0041-the-streak-beside-the-name.md), [0048](0048-the-daily-reminder-is-sent.md), [0049](0049-streak-milestones-in-the-feed.md) and [0050](0050-categories-and-badges-that-are-earned.md) |
| [0033](0033-invite-links-for-new-and-existing-accounts.md) | Invite links that work, for new and existing accounts | Accepted, amended by [0034](0034-bearer-tokens-for-the-native-app.md) |
| [0034](0034-bearer-tokens-for-the-native-app.md) | The iOS app: a client-only build that signs in with bearer tokens | Accepted |
| [0035](0035-dos-and-donts.md) | Dos & Don'ts: one name for what you commit to | Accepted, segments revised by [#77](https://github.com/AaronGreiner/q2/issues/77) |
| [0036](0036-live-or-screenshot.md) | Live or screenshot: every proof says which it is | Accepted |
| [0037](0037-donts.md) | Don'ts: a limit per period, logged uses and an honest relapse | Accepted, amended by [0038](0038-spot-checks.md) and [0044](0044-editing-a-running-goal.md) |
| [0038](0038-spot-checks.md) | Spot checks: a live photograph at a moment nobody can plan for | Accepted |
| [0039](0039-first-run-onboarding.md) | A first-run introduction, once, and a first Do that waits for a friend | Accepted |
| [0040](0040-back-goes-where-you-came-from.md) | Back goes where you came from, and finds the screen as it was left | Accepted |
| [0041](0041-the-streak-beside-the-name.md) | The streak beside the name, and a start screen of your own goals only | Accepted, amended by [0045](0045-what-waits-for-you-first.md) |
| [0042](0042-field-errors-as-codes.md) | Field errors travel as codes, and the app words them | Accepted |
| [0043](0043-threads-a-page-at-a-time.md) | A thread is read a page at a time | Accepted, amended by [0046](0046-looking-after-a-conversation.md) |
| [0044](0044-editing-a-running-goal.md) | A running goal is edited in front of the people who check it | Accepted |
| [0045](0045-what-waits-for-you-first.md) | What waits for you first, and the challenge as a strip of pictures | Accepted |
| [0046](0046-looking-after-a-conversation.md) | Looking after a conversation: correcting, taking back, reporting, pinning, and its photographs | Accepted |
| [0047](0047-one-name-on-screen.md) | One name on screen: Qdos | Accepted |
| [0048](0048-the-daily-reminder-is-sent.md) | The daily reminder is sent | Accepted |
| [0049](0049-streak-milestones-in-the-feed.md) | Streak milestones in the feed, and no percentage anywhere | Accepted |
| [0050](0050-categories-and-badges-that-are-earned.md) | Five categories instead of icons, and badges that are earned | Accepted |
| [0051](0051-a-first-window-that-can-be-met.md) | A first window that can be met | Accepted |
| [0052](0052-pauses-planned-ahead-and-for-everything.md) | Pauses planned ahead, and for everything at once | Accepted |
| [0053](0053-a-bio-and-one-streak.md) | A bio, and one streak | Accepted |
| [0054](0054-the-zone-your-days-are-counted-in.md) | The zone your days are counted in | Accepted |
| [0055](0055-why-a-photograph-was-doubted.md) | Why a photograph was doubted, said anonymously | Accepted |
| [0056](0056-the-challenge-has-a-moment.md) | The daily challenge has a moment | Accepted |
| [0057](0057-who-can-find-you-and-whether-your-activity-shows.md) | Who can find you, and whether your activity shows | Accepted |
| [0058](0058-your-data-as-one-file.md) | Your data as one file | Accepted |
| [0059](0059-a-do-that-ends-at-a-time-of-day.md) | A Do that ends at a time of day | Accepted |
| [0060](0060-video-and-both-cameras.md) | A proof can be a clip, and come from both cameras | Accepted |
| [0061](0061-both-cameras-in-step.md) | Both cameras in step, in either order, with the corner out of the way | Accepted |
