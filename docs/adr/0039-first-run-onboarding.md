# 0039 — A first-run introduction, once, and a first Do that waits for a friend

**Status:** Accepted
**Date:** 2026-10-12
**Issue:** [#93](https://github.com/AaronGreiner/q2/issues/93), and the handle
choice from [#108](https://github.com/AaronGreiner/q2/issues/108)
**Amends:** [0027 — Every goal is checked in its own conversation](0027-goal-conversations.md),
by adding what somebody without a friend can make instead of a goal.

## Context

A new account landed on a start screen that sent it into a wall. The start
screen pointed at Dos, the plus under Dos said "Lade zuerst einen Freund
ein", and since 0027 a goal cannot exist without a friend to check it. Nothing
said what Qdos is, and the first screen greeted a stranger with "Willkommen
zurück". The photograph, the handle, the accent, the push permission and the
invite link were each found by accident, if at all, and the handle, which
can never change, was never even shown before it was fixed.

The author asked for an introduction every account goes through once, modelled
on apps that do this well, and decided four things:

- **Content:** what Qdos is, the profile (photograph, name, handle), appearance
  and notifications, the first Do, the invite link, and a short tour.
- **A first Do without friends is a draft that waits** and starts by itself when
  the first friend arrives.
- **Mandatory, with every optional step skippable.**
- **Existing accounts go through it once as well.**

## Decision

### Once per account, remembered on the server

`Person.OnboardedAt` records when somebody finished it, and `SessionResponse`
carries `IsOnboarded`. The route guard (`middleware/auth.global.ts`) takes a
signed-in person who has not finished it to `/onboarding` from every screen,
and takes somebody who has finished it away from `/onboarding`. The open routes
(`/reset-password`, `/diagnostics`) and invite links (`/join/…`) stay reachable
either way. A reset link has to work, and an invite link somebody was sent is
the reason they are here.

On the person rather than in a cookie or local storage, because "only the first
time" has to hold across devices. The iOS app and the browser are two devices,
and somebody who went through it in one must not meet it in the other.

The migration leaves the column empty for every existing row, so **existing
accounts see it once**, as the author decided. Seeds mark their people as done
(`SeedBuilder.AddPerson`, `onboarded: true` by default), because a seeded world
stands for people who have been using q2 for months, and every suite signs in
as one of them. ManualTesting adds one person who has not been through it,
`nora.neu@kudos.example`.

### The steps

| Step | What it does | Can be put off |
| --- | --- | --- |
| Welcome | Three slides: a promise made in front of friends, a photograph instead of a tick, a streak. Illustrated with the app's own parts. Language switch on the first screen. | Skipped as a whole |
| Profile | Photograph, name, and the handle, shown and choosable | Photograph optional; name and handle are prefilled |
| Appearance | Light/dark, accent, language, with a preview of a start-screen row whose camera button is the accent | Defaults are preselected |
| First Do | Title (or an idea), rhythm, reminder, and the checkers when there are friends | "Später" |
| Invite | The link, with the waiting Do on screen as the reason to send it | Without a friend: "Erstmal ohne Freunde" ends the introduction here, and the waiting Do lapses |
| Notifications | What would be sent and when it stays quiet, then the browser's prompt from one tap | "Später"; the step is left out where push cannot work |
| Done | What is now set up, as states, and the way into the app | — |

The step is in the address (`?step=`). A reload stays where it was, and the
back gesture goes one step back. Every step writes through the composables the
rest of the app uses. The only things specific to the introduction are the
handle and its end (`/api/onboarding/handle/check`, `PUT /api/onboarding/handle`,
`POST /api/onboarding/complete`).

The order follows what the successful introductions do: the value first,
before anything is asked. Personalisation comes next, because it is cheap and
makes the app feel like one's own. Then the commitment, so that the invite link
is sent *for* something. The permission prompt comes last, framed by what it
is for. The browser's notification prompt is never shown without the
pre-permission screen in front of it. "Blockieren" is final, and only the
browser's settings undo it.

Notifications are offered only where asking can work and has not been
answered. They are not offered in the iOS app (no Web Push yet, #12), in a
browser without push, or on a deployment without VAPID keys. They are also not
offered in a browser that already said yes or no.

### The handle may be chosen while the introduction is open

The handle is derived from the name at registration and is fixed afterwards,
because it is what friends search for and write down (README section 2). The
introduction is the one moment its owner sees it before that matters, so it can
be changed there, and only there. `Person.ChooseHandle` refuses once
`OnboardedAt` is set. A chosen handle has 3 to 39 characters: lowercase
letters, digits, dots, hyphens or underscores, with a letter or a digit at
each end and never two separators in a row (`Handles`). Availability is checked
while typing. The unique index stays the authority. A lost race answers
"taken".

For an existing account going through the introduction once, this is also its
one chance to choose. Its friends may have written the old handle down. The
author accepted that when deciding that existing accounts go through it too,
and search by name still finds them.

### A first Do without friends waits, as a draft and not as a goal

`GoalDraft` holds one Do per person: title, icon, a repeating schedule and a
reminder. It is not a goal in a new status. It has no windows, no conversation
and no participants, and every query that reads goals would otherwise have to
learn to step around one that has none of those things. 0027 stays true:
**every goal is made in front of somebody and has its conversation from the
first moment.**

- **Only while there is no friend.** `PUT /api/goals/draft` is refused once
  there is a friend. With somebody there, the goal is made properly, with them
  chosen to check it.
- **It repeats.** A one-off has a day, and nobody knows on which day the first
  friend arrives.
- **Or it lapses, if somebody does not want to invite anybody yet.** On the
  invite step, while there is no friend and the link has not gone out,
  "Erstmal ohne Freunde" takes the place of "Später". After a question that
  names the Do, the draft is deleted. Then the introduction is completed, and
  the app opens with the tour, skipping the remaining steps. The Do goes first:
  were the introduction ended first and the discard then failed, a Do the
  person had let go of would start by itself the day a friend turned up.
  Whoever shares the link instead keeps it waiting. The author decided this on
  2026-10-16.
- **It starts by itself.** `GoalDraftStarter` runs wherever a friendship
  becomes accepted: `FriendsService.AcceptAsync`, a mutual request in
  `RequestAsync`, and `InviteService.BefriendAsync` (both the link at
  registration and the link for an existing account). It runs before the
  caller saves, so the friendship and the goal it made possible are committed
  together. The friend who arrived is put on it and gets the ordinary goal
  invitation. The goal's `CreatedAt` is the moment it started, and its first
  window opens then. The owner's screens follow through the `Goals` live
  change, which now also refreshes `goal-draft`.
- **One way to start a goal.** `GoalLauncher` is what `GoalService.CreateAsync`
  and the draft starter both go through: first window, conversation, feed
  line, invitations, metric. A goal made either way cannot differ in what came
  with it.

The draft is shown where the dead end was. It appears on the start screen as
"Dein erster Schritt" while there are no friends and nothing is due, next to
the invite link. It appears at the top of the Dos tab. The create sheet of a
friendless person offers to keep one waiting instead of a form that could only
be refused.

### The tour

After the last step the app opens at `/?tour=1`, and `AppTour` (in the default
layout) walks over the five tabs, one sentence each, lighting the real tab and
dimming the rest. Nothing is stored. It happens once because the introduction
does, and ending it removes the parameter.

### Copy

The sign-in screen no longer greets a stranger as a returning user ("Anmelden",
with a sentence on what Qdos is for), and the invite page says what Qdos is
before how the link works.

## Consequences

- One migration (`Onboarding`) adds `People.OnboardedAt` and the `GoalDrafts`
  table, cascade-deleted with the person. docs/privacy.md lists both; a
  draft's title is user content and is never logged.
- E2E accounts that are registered fresh and stand for existing users finish
  the introduction through the API (`invite.spec.ts`); `onboarding.spec.ts` is
  the one that walks through it.
- The draft is a Do only. A Don't's limit and proof mode are a later decision,
  made when somebody asks for one before having a friend.
- Push in the iOS app is #12. Once it exists, the notifications step needs a
  native permission path. It is left out there until then.
