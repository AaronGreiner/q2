# 0028 — A verdict by swipe, a vote you can take back, and every photograph full size

**Status:** Accepted
**Date:** 2026-09-24
**Amends:** [0018 — A window is closed by other people](0018-proof-and-vote.md),
where a vote was cast once and never changed.

## Context

Three things stood between a friend and a verdict that felt right.

- **The start screen only pointed at the vote.** A banner said "2 Beweise
  warten auf dein Urteil" and led to `/vote`. The one thing in q2 with a
  twelve-hour deadline on it was a tap away from the screen people actually
  open.
- **A vote was final.** 0018 fixed it because a vote revised after seeing the
  tally would be "a negotiation, not an opinion". In practice the commoner
  revision is a mistap, or a second, closer look at the picture in the goal's
  conversation. A final vote protects against the rare case by making the
  ordinary one impossible to correct.
- **A photograph could not be looked at.** A proof is judged on detail, and the
  only view of it was a square in a card. A challenge picture in the archive
  was a thumbnail with a date and no prompt, so after a few weeks nobody could
  tell what it had answered.

## Decision

### The stack is on the start screen

`ProofSwipeStack` replaces the banner. It shows the same queue as `/vote`
(`usePendingProofs`, key `proofs-pending`), one card at a time: **right
confirms, left doubts**. It disappears when nothing is waiting. `/vote` stays,
with the same stack, as the landing point of a notification.

Rules from 0018 that still apply:

- **No skip.** A swipe changes *how* a verdict is given, not whether one is.
- **The buttons stay.** A gesture is invisible to a screen reader and hard for
  anybody who cannot drag, so a verdict never depends on one. A pressed button
  sends the card off the same way a swipe does, so the two look like one path.
- **Only a horizontal drag is taken.** The first few pixels decide the axis. A
  vertical drag goes back to the page, so the start screen still scrolls.

Doubting is shown in the neutral inverted colour, not red: red means something
final ([0015](0015-qdos-design-language.md)), and one doubt is not a
rejection.

### A vote may be changed until the photograph is decided

`ProofPhoto.CastVote` replaces an earlier vote by the same person instead of
refusing it. It is still one vote per person, and it still counts once.
Nothing is accepted:

- once the photograph is `Confirmed` or `Rejected`, or
- once its deadline has passed, **even if the maintenance pass has not settled
  it yet**. After the deadline, only the votes already cast count, so a vote
  cast or changed at hour thirteen must not decide it.

So a verdict, a streak and a balance never move after the fact. Nothing
downstream has to be recomputed.

`VoteSummaryResponse.CanIVote` now means "may cast *or change*". With `MyVote`
set, the client shows what was chosen and one quiet button for the other
answer. That happens in the goal's conversation. The stack only holds
photographs not voted on yet.

What this does **not** open up:

- **A decided photograph cannot be changed.** A goal with one friend is decided
  by that friend's first vote ("everybody has spoken"), so there is nothing to
  change there. Changing a vote matters on goals with several friends, while
  some have not voted yet.
- **Anonymity is unchanged.** A confirmer who switches to doubt drops out of
  `ConfirmedBy`. That is the same information a doubt has always given away:
  the count went up and a name went down. The doubter's id is still never
  loaded (`ProofsEndpointTests.AVoteMayBeChangedWhileTheVoteRuns`).

### Every photograph opens full screen

`AppPhotoViewer` is mounted once in `app.vue`, outside both layouts, and
driven by `usePhotoViewer`. Proof photographs, challenge contributions,
archive tiles and avatars all open into it.

- **It draws its own pinch-zoom.** [0013](0013-app-like-input.md) turns
  browser zoom off for the whole document, and that stays. The viewer takes
  every touch itself (`touch-action: none`) and scales only the picture, so a
  photograph can be looked at closely without the app zooming out of its own
  layout. Double-tap zooms in and out, and a drag down closes the view. So do
  the close button and Escape.
- **What the picture is of goes with it.** At the bottom it shows the goal's
  title, or the day's challenge prompt, then whose picture it is and when. The
  bar hides while zoomed in.
- **Avatars expand only where it makes sense** (`expandTitle`): on the profile
  and person pages, a goal's team, the chat header and the cards. Most avatars
  sit inside a row that is already a link, where a second target under the
  same thumb would be a mistap waiting to happen.

The archive tile now shows the prompt under the picture, cut to two lines. The
full prompt is in the viewer.

## Consequences

- The start screen now needs `usePendingProofs`. The count on the bell is
  unaffected.
- The viewer is one more overlay. It closes on navigation, so a notification
  tapped while it is open does not land underneath it.
- The Android back gesture does not close the viewer. It navigates, and the
  viewer closes with the page it was on.
