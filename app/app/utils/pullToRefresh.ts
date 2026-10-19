/**
 * The arithmetic of pulling a list down to read it again (#19), apart from
 * the touch events so it can be tested without a finger.
 *
 * The indicator follows half of the finger's travel, so the pull feels like it
 * has weight and a long thumb movement is not a long jump on screen, and it
 * never travels further than {@link PULL_MAX}. Letting go at or beyond
 * {@link PULL_THRESHOLD} refreshes; anything less springs back and does
 * nothing.
 */

/** How far the indicator has to travel, in CSS pixels, before letting go refreshes. */
export const PULL_THRESHOLD = 64

/** The furthest the indicator travels. */
export const PULL_MAX = 96

/**
 * How far a finger has to move before the gesture decides whether it is a
 * pull or something else — a sideways swipe on a proof card, say.
 */
export const PULL_SLOP = 8

/** The finger's downward travel, as the distance the indicator moves. */
export function pullDistance(travel: number): number {
  if (!(travel > 0)) return 0
  return Math.min(PULL_MAX, travel / 2)
}

/** Whether letting go at this distance refreshes. */
export function isPullArmed(distance: number): boolean {
  return distance >= PULL_THRESHOLD
}

/**
 * Whether a movement is a pull down at all: mostly vertical and downwards.
 * Decided once, after {@link PULL_SLOP}, so a swipe that drifts does not turn
 * into a pull halfway.
 */
export function isPullGesture(dx: number, dy: number): boolean {
  return dy > 0 && Math.abs(dy) > Math.abs(dx)
}
