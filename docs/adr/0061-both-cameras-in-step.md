# 0061 — Both cameras in step, in either order, with the corner out of the way

**Status:** Accepted
**Date:** 2026-10-23
**Issue:** [#151](https://github.com/AaronGreiner/q2/issues/151)
**Amends:** [0060 — A proof can be a clip, and come from both cameras](0060-video-and-both-cameras.md)

## Context

Since 0060 a proof can come from both cameras, but three things about it were
fixed where the people using it wanted a choice.

- **The order.** The rear picture always came first. Somebody who wants their
  own face before turning the phone round had no way to say so.
- **The corner.** The front picture sat in the top-left corner and covered
  whatever was underneath it, so the large picture could never be seen whole.
- **A clip.** A clip from both cameras was the rear clip plus a front *still*,
  taken when it stopped. That shows who filmed it, but not what happened on
  both sides of the phone during those seconds, which is what "both cameras"
  promises on video.

## Decision

### What was decided with the owner

- **The order can be swapped before taking it**, with a tap on the corner in
  the camera. This changes what is stored only in order, not in role: **the
  rear camera is always the main part**, every viewer starts with it large and
  can swap with a tap, as before. The swap is not remembered, so each opening
  starts with the rear camera first.
- **The small picture can be dragged by whoever is looking**, along the top,
  and lands in whichever top corner is nearer — "von oben links nach oben
  rechts", the way BeReal does it. This works in the camera, the preview and
  wherever a proof is shown large. Where it sits is not stored. Tiles keep
  their fixed corner.
- **A clip from both cameras is filmed from both sides at once, the way BeReal
  does it**, and is seen as one picture whose small half can be swapped and
  dragged like a photograph's. This holds where the browser can run both
  cameras. Elsewhere — an iPhone in Safari and in the app, most Android phones
  in a browser — it stays as 0060 left it, and native simultaneous capture
  stays [#143](https://github.com/AaronGreiner/q2/issues/143).

### How

- **Two clips, not one composited on the phone.** A clip drawn into one
  picture on the phone would burn the small picture into place, and then
  nobody could move it out of the way or swap it, which is the point. So the
  front camera records its own clip, started in the same moment as the rear
  one by the same press, with its own copy of the sound and its own still.
  `ProofPhoto` gains `InsetMedia` (whether `InsetImageId` is a photograph or a
  clip, read off the upload's bytes like `Media`) and `InsetPosterImageId`.
  `ProofParts` holds the rules: a front clip only beside a clip, always with a
  still, and a still only for a front clip; every part is a picture of its
  own; and, as before, only live. The still has the proof's audience, goes
  with the goal and the account, and is in the export (`FrontCameraStill`).
  One migration, `BothCamerasInStep`, marks every earlier front picture as a
  photograph.
- **Played as one** (`utils/clipsInStep.ts`). The large clip leads: it has the
  player's controls full screen, the sound button on a card, and the loop.
  The corner follows it: it plays and stops with it, and is put back on its
  moment after a seek or a drift of more than a quarter of a second, which no
  eye catches in a corner and which avoids the stutter a tighter correction
  causes. The corner is always silent. A clip moved into the corner keeps
  playing there, a clip beside a front photograph included; this replaces
  0060's "a clip by its still in the corner", which would have stopped one
  half of the picture whenever it was swapped.
- **Two layers that trade places** (`ProofMediaFrame`). The main part and the
  front part each keep their own element. A swap only moves them, so a clip
  carries on from where it was instead of loading again. A transparent button
  laid over the corner takes the tap and the drag (`useInsetCorner`): the
  press stops there, so it is never also a vote swiped on the card or the
  drag that closes the full screen. A drag swallows the click that ends it,
  and the arrow keys move the corner too. The full-screen viewer draws through
  the same frame now, passing its zoom to the large photograph, and keeps the
  corner below the status bar and, in the end corner, below its close button.
- **The camera** (`PhotoCapture`). Where both run at once, a tap on the corner
  trades the two live viewfinders without opening anything again. Where they
  run in turn, front first opens the front camera, and the shutter takes it,
  then "Jetzt hinten" with the same countdown, then the rear one. For a clip,
  the rear camera starts recording on its own after the countdown, so one
  press is still all it takes. The first picture waits in the corner while
  the second is taken. A camera that will not open for the second part leaves
  the one picture that was taken, and says so.

## Consequences

- Two more nullable columns on `ProofPhotos`, and a front clip counts against
  the clip allowance like any clip: about twice the bytes of a single clip.
- Two clips play in step, not in lockstep. A quarter of a second apart is the
  most they drift before being corrected, which is enough for a picture and
  not for lip-sync across the two. The sound comes from the large clip only.
- On the phones most people have, nothing changes for a clip until #143. The
  order and the corner work everywhere.
- What this cannot settle without hardware — two clips recorded together on
  an Android phone that allows it, and playback of two clips at once in
  Safari and in the app — is checked on a device and recorded on #151.
