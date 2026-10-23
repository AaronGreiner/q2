/**
 * Where the small picture of a proof from both cameras sits (#151).
 *
 * It covers part of the large one, so whoever is looking can drag it out of
 * the way: along the top, and it lands in whichever top corner is nearer when
 * it is let go — the way BeReal does it. Only the two top corners, because the
 * bottom of every frame already carries something (the live/screenshot label,
 * the sound button, a player's controls).
 *
 * q2 is written left to right, so "start" is the left corner and a drag to the
 * right is a positive distance.
 */

export type InsetSide = 'start' | 'end'

/** Movement below this, in pixels, is a tap — which swaps the two pictures — not a drag. */
export const insetTapSlop = 8

/**
 * How far the small picture follows a drag of `dx` from `from`, with `travel`
 * pixels between the two corners: never out of its frame.
 */
export function clampDrag(from: InsetSide, dx: number, travel: number): number {
  const room = Math.max(0, travel)

  return from === 'start' ? Math.min(room, Math.max(0, dx)) : Math.max(-room, Math.min(0, dx))
}

/** The corner a drag of `dx` from `from` lands in: whichever is nearer when it is let go. */
export function settleSide(from: InsetSide, dx: number, travel: number): InsetSide {
  const position = (from === 'start' ? 0 : travel) + clampDrag(from, dx, travel)

  return position > travel / 2 ? 'end' : 'start'
}

/**
 * The distance between the two corners for an element that sits in one of
 * them: the width it may cross, keeping the same margin on either side.
 */
export function travelOf(element: Pick<HTMLElement, 'offsetLeft' | 'offsetWidth'>, frameWidth: number, side: InsetSide): number {
  const margin = side === 'start'
    ? element.offsetLeft
    : frameWidth - element.offsetLeft - element.offsetWidth

  return Math.max(0, frameWidth - element.offsetWidth - 2 * Math.max(0, margin))
}
