# 0060 — A proof can be a clip, and come from both cameras

**Status:** Accepted, amended by [0061](0061-both-cameras-in-step.md) (the order, the corner, and a clip from both cameras at once)
**Date:** 2026-10-23
**Issues:** [#124](https://github.com/AaronGreiner/q2/issues/124),
[#125](https://github.com/AaronGreiner/q2/issues/125)
**Amends:** [0017 — Image storage](0017-image-storage.md) (a clip is stored
behind the same seam, with ranges), [0018 — Proof and vote](0018-proof-and-vote.md)
(a proof is more than one picture), [0036 — Live or screenshot](0036-live-or-screenshot.md)
(a clip and both cameras are only ever live), and
[0038 — Spot checks](0038-spot-checks.md) (a check may be answered with either).

## Context

A proof was one photograph. A photograph catches one moment of something that
only shows as movement — a set of push-ups, the last metres of a run — and
friends have to take the rest on trust (#124). And it shows what was done but
not who did it: a picture of a gym could have been taken by anybody (#125).

Both are answered by the camera, not by a rule. A few seconds of video are
much harder to stage than a still, and BeReal's two pictures at once — the
rear camera on the scene, the front camera on the person — put the owner in
their own proof.

## Decision

### What was decided with the owner

- **A clip is at most ten seconds, with sound**, which plays without sound
  until somebody asks for it.
- **A clip is only ever live.** The picker stays a picture's route (a
  screenshot, ADR 0036); a clip from the phone's library would mostly be an
  iPhone's HEVC `.mov`, longer than ten seconds, and without transcoding it
  could only be refused.
- **Both cameras are two pictures**, stored apart and linked to the proof —
  not one picture composited on the phone — so either can be the large one,
  swapped with a tap.
- **Both cameras is on whenever the phone has two, at every opening**, and can
  be switched off for one capture. It is not remembered: the default is the
  point. It applies to a clip as much as to a photograph.

### How

- **Storage (amends 0017).** A clip is a `StoredImage` with `ContentType`
  `video/mp4` and a `DurationMilliseconds`, behind `IImageStore` like any
  picture: no public address, a session plus `CanRead`, `private, no-store`,
  404 rather than 403. Only a proof may be one. The server reads the ISO base
  media boxes (`Mp4Reader`) — never the `Content-Type` header — and accepts
  H.264 (`avc1`/`avc3`) with optional AAC sound, because that is what every q2
  client plays; it takes the length from the movie header, the fragment header
  or the fragments' runs, since `MediaRecorder` writes fragmented MP4 whose
  header may say nothing. **No transcoding and no ffmpeg.** Limits: 12 MB per
  clip, ten seconds plus one for a recorder that stops a frame late, and a
  share of 200 MB out of a 300 MB allowance, so pictures keep the 100 MB they
  always had. `GET /api/images/quota` reports all of it.
- **Ranges for clips only.** Safari plays a `<video>` only from a server that
  answers byte ranges; a photograph is still fetched whole.
- **A proof has parts (amends 0018).** `ProofPhoto` keeps its `ImageId` — the
  photograph or the clip — and gains `Media`, `PosterImageId` (a clip's still,
  taken by the phone as recording starts, because the server never decodes
  video) and `InsetImageId` (the front camera's picture). `ProofParts` holds
  the rules: a clip has a still and a photograph none, every part is a
  picture of its own, and a clip or a front picture requires
  `ProofSource.Live` (amends 0036) — a client that calls either a screenshot
  is refused. The service checks each id is the owner's own proof upload and
  reads whether the main one is a clip from the stored row. The still and the
  front picture have exactly the proof's audience, go with the goal when it is
  deleted, are in the data export, and go with the account (ADR 0022).
- **Voting is unchanged.** A clip, with or without a front picture, is judged
  exactly like a photograph.
- **Recording.** `MediaRecorder` with H.264 and AAC where the browser has
  both, H.264 alone where it has no AAC encoder (a silent clip beats none), and
  plain `video/mp4` for Safari. A browser that records none of these gets no
  video mode. The microphone is asked for apart from the camera and given up
  on after four seconds, so a prompt that never answers cannot hold up the
  camera. The shutter starts and stops a clip — a tap rather than holding, so
  it works from a keyboard and a screen reader as well as a thumb.
- **Both cameras.** The camera first tries to run the front camera beside the
  rear one. Where both stay live — a desktop with two cameras, some Android
  phones — they are both on screen and one press takes both. Most phones in a
  browser cannot (WebKit allows one camera capture at a time), so a device that
  said no is remembered and the pictures are taken in turn: the rear one, then
  "Jetzt du" with a short countdown, then the front one. A clip with both
  cameras gets the front picture when it stops. The front camera is mirrored
  while live and never in what is kept.
- **Showing it.** `ProofMediaFrame` is the one layout, used by the card, the
  swipe stack, the conversation, the full-screen views, the gallery and the
  conversation's grid, and by the camera's own preview: a clip plays without
  sound while on screen on a card, with the player's controls full screen,
  and as its still with a play mark on a tile; the front picture sits in a
  corner and swaps on a tap. Every element that can show either carries
  `data-q2-block` (ADRs 0005 and 0017). In the iOS app a clip is fetched with
  the bearer token and played from memory, like every picture (ADR 0034).

## Consequences

- Two nullable columns and one text column on `ProofPhotos`, one nullable
  column on `Images`. Every proof before this is a single photograph.
- A dual proof on a phone is two moments a second or two apart, not one. That
  is BeReal's compromise too, and the screen says what is happening.
- A front camera's moving picture beside a clip needs simultaneous capture,
  which on an iPhone means `AVCaptureMultiCamSession` through a native plugin
  — left for its own issue once dual proofs are in use.
- A clip recorded on a browser whose default MP4 codec is not H.264 is refused
  at upload with its own sentence rather than stored and left unplayable.
- What this cannot settle without hardware — recording, playback and sound on
  a real iPhone and Android phone, in Safari, Chrome and the app, and which
  route each takes for both cameras — is checked on a device and recorded on
  #124 and #125.
