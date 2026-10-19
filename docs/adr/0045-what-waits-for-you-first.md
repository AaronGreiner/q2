# 0045 — What waits for you first, and the challenge as a strip of pictures

**Status:** Accepted
**Date:** 2026-10-19
**Issues:** [#135](https://github.com/AaronGreiner/q2/issues/135)
**Amends:** [0021 — The daily challenge](0021-daily-challenge.md), where the
start screen carried the challenge as a banner that only led into the room, and
[0041 — The streak beside the name](0041-the-streak-beside-the-name.md), whose
start screen opened on the next step.

## Context

- **A friend's photograph was easy to scroll past.** The vote stack sat below
  the next step, below the first-run card and below the challenge banner. Of
  everything on the start screen it is the one thing with somebody else's
  deadline on it: the twelve hours of [0018](0018-proof-and-vote.md) run
  whether or not anybody looks.
- **The challenge said nothing of what friends had done.** The banner was a
  prompt, a counter ("3 von 8 Freunden dabei") and a pill, and it led into the
  room, because the room was the only place the pictures were. A banner you
  could contribute from would have skipped them, so it had no camera of its own.

## Decision

### The order is the order things want something from you

1. An open **spot check** ([0038](0038-spot-checks.md)), which is gone in three
   minutes and rare enough never to crowd anything out.
2. **The vote stack**, whenever something waits for your verdict.
3. **The daily challenge.**
4. **Your own day**, unchanged: the evening's risk cards or "Als Nächstes",
   "Heute", then the friends' feed.

The first-run card ([0039](0039-first-run-onboarding.md)) keeps its place above
the challenge. It is only shown to somebody with no friends, who has no vote
stack either, and an empty room is not a better first step than the invite.

Everything on the screen sits in one wrapper (`home-sections`), so `first:mt-0`
counts from the first section again. Since pull-to-refresh
([#19](https://github.com/AaronGreiner/q2/issues/19)) the panel's first child is
the pull indicator, and whatever came first kept its top margin.

A pull now reads the vote stack and the challenge again as well as the
dashboard: they are the first two things on the screen and the two that change
while it is open.

### The challenge is a card with the pictures in it

`ChallengeCard` replaces `ChallengeBanner`: the prompt set like a headline, the
time left, and a strip of portrait tiles (`ChallengeStripTile`), one per
contribution — yours first, because it is what uncovered the rest, then your
friends' **newest first**. The room lists them oldest first, like a chat that
fills up over the day; a strip is cut off at the right edge, and what is cut
off should be what you already saw.

A tap on a picture opens it full screen with the prompt
([0030](0030-swipe-vote-and-full-screen-photos.md)). Reactions, replacing and
removing stay in the room, which the card's heading opens: a row of buttons
under a tile 116 pixels wide would be a row of things to miss.

### Covered is still covered, and the camera moves onto the card

The reciprocity rule of [0021](0021-daily-challenge.md) is unchanged and still
enforced by what the server sends. Before you have contributed, a friend's tile
shows their avatar and name and no picture, because there is none in the
payload. A blurred preview was considered and left out for the reason 0021
gives: a blur over bytes that were sent anyway is a curtain with a gap in it.

What changes is where you join. The reason the banner had no camera — the
pictures were only in the room — no longer holds, so "Mitmachen" opens the
camera on the start screen, and the contribution's answer, which is the whole
room again, uncovers the strip without a second read. The accent is on that
button and nowhere else on the card; once you are in, "Erledigt" is grey.

## Consequences

- `ChallengeBanner` is gone; `ChallengeCard` and `ChallengeStripTile` are new.
  The start screen has a third camera overlay, for `ChallengeEntry`, next to the
  proof's and the spot check's.
- No contract change: the room response already carried everything the strip
  draws.
- When the challenge appears and how people are told about it are not decided
  here; that is [#136](https://github.com/AaronGreiner/q2/issues/136).
