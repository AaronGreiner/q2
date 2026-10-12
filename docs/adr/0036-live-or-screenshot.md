# 0036 — Live or screenshot: every proof says which it is

**Status:** Accepted
**Date:** 2026-10-09
**Amends:** [0018 — A window is closed by other people](0018-proof-and-vote.md),
which recorded `CapturedInApp` and left it at that, and
[0030 — A verdict by swipe, … every photograph full size](0030-swipe-vote-and-full-screen-photos.md),
whose full-screen view did not say where a picture came from.

## Context

Some commitments can only be shown on a screen: screen time, a finished
online course, a streak in a language app. A picture from the phone's library
could always be delivered, through the file picker `PhotoCapture` offers for
phones without a working camera. But nothing honest recorded that it was one.
`ProofPhoto.CapturedInApp` existed, yet the client sent it as `true` for
every proof, gallery picks included, so "Aus der Galerie" never appeared and
the stored flag meant nothing.

0018 rests on the point that a picture you can pick from your gallery is not
evidence of anything by itself. The friends deciding whether a window counted
should always know which kind they are looking at.

## Decision

- **Both routes, every time.** Taking a proof always offers the live camera
  and a screenshot (any picture from the phone). No per-goal setting restricts
  it. The one exception is the spot check on a Don't, which is live only
  ([#63](https://github.com/AaronGreiner/q2/issues/63)).
- **The source is recorded at upload and never changes.** `ProofPhoto.Source`
  is `Live`, `Screenshot` or `Unknown`, stored as text. `PhotoCapture` reports
  the route it used (`camera` or `library`) to its caller, and the proof sends
  it as `source`. No endpoint writes to a photograph afterwards.
- **It is shown everywhere a proof is shown**, as one `ProofSourceBadge`
  (a word and an icon, no colour): on the vote card and in the swipe stack, in
  the goal's conversation, in the full-screen view, and on the profile's
  gallery tile and its full-size view. The camera screen shows the label
  before the picture is sent, so the owner knows what their friends will
  read.
- **A screenshot is fitted, not cropped**, wherever proofs are square tiles,
  because a square cut out of a tall picture full of text is the part that
  says nothing.
- **Voting is unchanged.** Both sources are judged by the rules in 0018.

### Existing proofs are `Unknown`

Mapping the old flag would have labelled every gallery pick "Live" after the
fact, because the flag was `true` for all of them. Every existing proof is
therefore migrated to `Unknown`, which the interface shows neutrally as
"Ohne Angabe". For the same reason, a request that names no `source` (which
includes builds installed from TestFlight before this change, which still
send the old `capturedInApp: true`) is stored as `Unknown` rather than
believed.

### What the label does not claim

The server receives bytes. It cannot tell a photograph taken a second ago
from one taken last year, and a modified client could send `Live` for
anything. "Live" therefore means "the app's camera was used", not "this is
proven fresh". The label helps friends judge, and they still decide. That is
the same honesty 0018 asks of the vote itself.

## Consequences

- `CapturedInApp` is gone from proofs: the contract carries `source` on
  `ProofResponse` and `OwnProofResponse`, and the request takes `source`.
- The daily challenge had the same always-true flag. It is not a proof, but
  it now records the same `Source` the same way
  ([#64](https://github.com/AaronGreiner/q2/issues/64)): `ChallengeEntry.Source`
  replaces `CapturedInApp`, existing entries are migrated to `Unknown`, and a
  request without `source` is stored as `Unknown`. The room keeps its own,
  plainer label: only a known gallery pick says "Nicht live aufgenommen", and
  an unknown entry says nothing, because nobody knows that it was one.
