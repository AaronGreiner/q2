# 0055 — Why a photograph was doubted, said anonymously

**Status:** Accepted
**Date:** 2026-10-20
**Issues:** [#111](https://github.com/AaronGreiner/q2/issues/111)
**Amends:** [0018 — A window is closed by other people](0018-proof-and-vote.md),
whose doubt was a bare "Anzweifeln".

## Context

A refused photograph told its owner that it was refused, not why: the wrong
place, the wrong day, an old picture, or one that shows nothing. They get one
more try and had to guess what would convince.

## Decision

- **A doubt can say what was missing**, from three fixed answers: "Zeigt
  nicht, was versprochen war", "Altes Bild oder Screenshot", "Nicht
  erkennbar" (`DoubtReason`). Optional — "Ohne Grund" is one of the choices,
  and a swipe to the left in the stack doubts without one, the quick way.
  "Anzweifeln" on a card now asks first; a doubt already cast can be given a
  reason, or have it changed, while the vote runs.
- **No free text.** A field anybody may type into anonymously is a field for
  insults, and the report path has nobody to answer it yet (#38). A fixed
  answer cannot be one.
- **Anonymous like the doubt.** The reason is stored on the vote row, which
  already holds who voted and is never shown. The photograph's owner reads the
  reasons **counted** ("Altes Bild oder Screenshot (2)") on the photograph and,
  as the most-given one, in the refusal's notification (`Notification.Detail`,
  a code the app words). A voter sees only their own. Nobody else sees any, and
  no response carries who gave which — an integration test reads every payload
  for the doubters' ids.

## Consequences

- Two nullable columns: `ProofVotes.Reason` and `Notifications.Detail`
  (migration `DoubtReasons`).
- With two doubters giving the same reason, the owner knows both gave it —
  which is what "counted" means, and no more than the doubt count already says.
