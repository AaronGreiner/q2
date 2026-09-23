# 0029 — Photographs in conversations, and no ready-made replies

**Status:** Accepted
**Date:** 2026-09-23
**Builds on:** [0017 — Image storage](0017-image-storage.md) and
[0027 — Every goal is checked in its own conversation](0027-goal-conversations.md).

## Context

The composer offered four one-tap encouragements above the message box
("Stark!", "Weiter so!", …). They made a friend's reply cost nothing, which is
exactly what made it worth nothing: the same four sentences, word for word,
from everybody. A conversation could also carry only words, while the one
thing a goal's friends most want to see of each other is a picture — and the
only pictures a thread could hold were proofs, which are votes, not messages.

## Decision

**The ready-made replies are gone.** What somebody sends a friend is what they
typed. Kudos on a message stay: they are a reaction, not a reply. The pinned
goal's "Anfeuern" stays too — it is one button with one meaning, on the goal.

**A message is words, a photograph, or both.** `ChatMessage.ImageId` points at
an image uploaded beforehand under the new `ImagePurpose.ChatPhoto`, the same
way a proof points at its picture: a column without a foreign key, so adding it
was a plain `ADD COLUMN` rather than a table rebuild. `SendMessageRequest`
takes an optional `imageId`; a message with neither text nor photograph is
still refused.

**The audience is whoever is in the conversation now.** `ImageService.CanReadAsync`
answers `ChatPhoto` from the message carrying it and the conversation's
participants — the same set that can read the thread. Somebody who leaves a
group stops being able to open its pictures when they stop being able to open
its messages.

**A photograph is sent once, and only by its owner.** Sending somebody else's
image id, or one uploaded for another purpose, is a 404; sending the same one
twice is a 400. Otherwise an id would be a way to widen a picture's audience,
and deleting either conversation would take it out of the other.

**A chat photograph is never a proof.** The owner's camera in a goal's
conversation still delivers into the window, with a vote; the photograph button
beside it sends a message. Two buttons, because they do two different things.

**Photographs go with their conversation.** Whatever removes a conversation
whole — deleting its goal, the last person leaving a group, an account erasure
taking a direct thread — removes every `ChatPhoto` in it, whoever sent it, and
their bytes after the commit (`ImageService.RemoveChatPhotosAsync`). Left
behind, they would be files nobody can open, still counted against a friend's
allowance.

## Consequences

- The chat list says "Foto" for a message that is only a photograph
  (`ChatSummaryResponse.LastMessageHasPhoto`), and a push for one falls back to
  "Neue Nachricht" — there are no words of the sender's to quote.
- An upload whose message then fails is deleted again by the client. If that
  tidy-up fails too, the picture stays its owner's alone and counts against
  their allowance, which is where any upload that is never sent ends up.
- A photograph deleted through `DELETE /api/images/{id}` leaves its message in
  place with `image: null`; a message that was only that photograph is drawn as
  "Foto nicht mehr verfügbar" rather than as an empty bubble.
- Seeds still write no image bytes, so no seeded conversation holds a
  photograph; the E2E suite sends one through the real sheet.
