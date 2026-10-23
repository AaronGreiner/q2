/**
 * Two clips filmed at the same moment, played as one (#151).
 *
 * Where both cameras film at once, a proof is the rear camera's clip and the
 * front camera's, stored apart so either can be the large one and the small
 * one can be dragged out of the way — the way BeReal shows both sides. On
 * screen they have to read as one picture, so the one in the corner follows
 * the large one: it plays when that plays, stops when it stops, and is put back
 * on the same moment when the two drift apart or somebody seeks.
 *
 * The large one leads because it is the one with the controls and the sound.
 * The two recordings start within a frame of each other, and a quarter of a
 * second either way is not something an eye catches in a corner; correcting
 * any closer would make the follower stutter instead.
 */

/** How far apart the two may drift before the follower is put back, in seconds. */
export const stepTolerance = 0.25

/** The part of a `<video>` this needs, so a test can hand in something smaller. */
export type StepClip = Pick<HTMLVideoElement,
  'currentTime' | 'paused' | 'duration' | 'playbackRate' | 'play' | 'pause' | 'addEventListener' | 'removeEventListener'>

/**
 * Keeps `follower` on `leader`'s moment until the returned function is
 * called. A follower that is a frame shorter waits at its end for the leader
 * to loop round rather than starting over on its own.
 */
export function keepInStep(leader: StepClip, follower: StepClip, tolerance = stepTolerance): () => void {
  function align() {
    if (Math.abs(follower.currentTime - leader.currentTime) > tolerance) {
      follower.currentTime = leader.currentTime
    }
  }

  function playAlong() {
    const end = Number.isFinite(follower.duration) ? follower.duration : Number.POSITIVE_INFINITY

    if (!leader.paused && follower.paused && leader.currentTime < end - tolerance) {
      follower.play().catch(() => {
        // Refused autoplay: the corner shows its frame, and the next tick
        // tries again.
      })
    }
  }

  function onPlay() {
    align()
    playAlong()
  }

  function onPause() {
    follower.pause()
    align()
  }

  function onTick() {
    align()
    playAlong()
  }

  function onRate() {
    follower.playbackRate = leader.playbackRate
  }

  const listeners: [string, () => void][] = [
    ['play', onPlay],
    ['pause', onPause],
    ['seeked', align],
    ['timeupdate', onTick],
    ['ratechange', onRate],
  ]

  for (const [type, listener] of listeners) leader.addEventListener(type, listener)

  onRate()
  align()
  if (leader.paused) follower.pause()
  else playAlong()

  return () => {
    for (const [type, listener] of listeners) leader.removeEventListener(type, listener)
  }
}
