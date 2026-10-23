# 0062 — The full camera everywhere but a profile picture

**Status:** Accepted
**Date:** 2026-10-23
**Issue:** [#152](https://github.com/AaronGreiner/q2/issues/152)
**Amends:** [0060 — A proof can be a clip, and come from both cameras](0060-video-and-both-cameras.md)
and [0061 — Both cameras in step](0061-both-cameras-in-step.md), which gave
all of this to a proof alone; [0021 — The daily challenge](0021-daily-challenge.md),
whose contribution was one photograph.

## Context

After 0060 and 0061 the camera behaved differently depending on where it was
opened from. For a proof it recorded clips, used both cameras, let the order be
swapped and the corner be dragged. For the daily challenge, a chat photograph
and the photograph on a Don't's logged use it was one camera and one
photograph. The daily challenge was the odd one out most of all: it is the
moment in q2 most like BeReal, and it had the least of it. The owner asked for
the camera to "always work like this, the daily challenge included".

## Decision

### What was decided with the owner

- **The full camera everywhere but the profile picture.** A challenge
  contribution, a chat photograph and a logged use each get exactly what a
  proof has: both cameras on at every opening, at once or in turn, the order
  swapped and the corner dragged, and photo or video. A profile picture stays
  one camera and one photograph, because a picture-in-picture cannot be an
  avatar and an avatar does not move.
- **Video too**, up to ten seconds with sound, two clips in step where both
  cameras run at once. A logged use gets it as well: "the camera always works
  the same" covers it, although only the challenge and the chat were asked
  about by name.
- **The challenge keeps the gallery, labelled "Screenshot"**, the way a proof
  does. The camera offers it as "Screenshot auswählen" and its preview shows
  the label. The card shows the screenshot badge instead of the old line "Nicht
  live aufgenommen" — still only for the exception, never "Live".

### How

- **One set of parts** (`MediaParts`, moved from `ProofParts` into the image
  feature). `ChallengeEntry`, `ChatMessage` and `DontUse` gain the same five
  columns a proof has: `Media`, `PosterImageId`, `InsetImageId`, `InsetMedia`
  and `InsetPosterImageId`. They are mapped once (`MediaPartsMapping`), and one
  migration, `TheFullCameraEverywhere`, marks every earlier row as a photograph.
  `ImageService.ResolvePartsAsync` checks each id as the owner's own upload for
  the purpose and reads clip or photograph off the stored bytes, for all four
  places alike.
- **Live where the route is recorded.** A challenge contribution records its
  route, so a clip or a second camera has to be live there, as for a proof. A
  chat photograph and a logged use record no route and are labelled with none,
  so there only the shape of the parts is checked (`Validate(…, live: null)`).
- **A clip for every purpose but an avatar** (`StoredImage.MayBeVideo`), within
  the same clip allowance.
- **Every part has its picture's audience, and goes with it.** The room's
  reciprocity covers every part; a chat photograph's parts belong to the
  conversation, and every part can be sent once only; a use's parts belong to
  the goal. Replacing or withdrawing a contribution, taking a message back,
  deleting a conversation, a goal or an account, and undoing a use remove every
  part. Before this, a use's photograph stayed on disk when the use was undone
  or its goal deleted; it goes with them now. The export names every part.
- **Drawn like a proof.** The challenge card, the strip and the archive, a chat
  bubble, the conversation's photo grid, a logged use's line and the full-screen
  viewer all go through `ProofMedia`. Tiles show the still, a play mark and the
  front part in the bottom corner. The card and the bubble play inline, with
  the corner swapping and moving.
- **The camera decides by purpose.** `PhotoCapture` no longer has a switch for
  the full camera. It is on for every purpose but `Avatar`. Callers receive
  everything that was taken (`uploaded` carries the `Captured` parts).

## Consequences

- Fifteen nullable or defaulted columns over three tables, and three indexes
  on `ChatMessages` for the parts, because a chat photograph's audience is
  answered from the message on every image request.
- A covered challenge entry still sends no picture: its parts are null until
  the viewer has taken part, like its main id. Only whether it is a clip is
  sent, which shows nothing of it.
- What needs hardware — recording on a phone that runs both cameras, and
  playing two clips in a chat bubble in Safari and in the app — is checked on
  a device and recorded on #152.
