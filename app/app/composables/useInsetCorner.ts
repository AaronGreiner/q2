import { type InsetSide, clampDrag, insetTapSlop, settleSide, travelOf } from '~/utils/insetCorner'

/**
 * Dragging the small picture of a proof from both cameras between the two top
 * corners, and telling a drag from the tap that swaps the pictures (#151).
 *
 * Bound to the corner's own button: a tap there is still its click, which
 * swaps; a drag moves it, lands in the nearer corner, and swallows the click
 * the finger would otherwise make on lifting. The arrow keys move it too, so
 * moving it never depends on a gesture alone (the rule the swipe vote and the
 * long press follow).
 *
 * The press stops where it starts. The corner sits on a card that swipes to
 * vote and in a viewer that drags down to close, and dragging the corner must
 * be neither. Vertical movement is left to the page (`touch-pan-y` on the
 * button), so a feed still scrolls from a finger that lands on it.
 *
 * Where it sits belongs to whoever is looking, here and now: it is not stored,
 * and a new proof starts top-left.
 */
export function useInsetCorner() {
  const side = ref<InsetSide>('start')
  const offset = ref(0)
  const dragging = ref(false)

  let press: { id: number, x: number, y: number, travel: number } | null = null
  let moved = false
  let tookClick = false

  /** Follows the finger; slides into its corner once let go. */
  const style = computed(() => ({
    transform: offset.value ? `translateX(${offset.value}px)` : undefined,
    transition: dragging.value ? 'none' : 'transform 180ms ease-out',
  }))

  function reset() {
    side.value = 'start'
    offset.value = 0
    dragging.value = false
    press = null
  }

  /**
   * Moves it to `landed` without a jump: the offset is first re-expressed from
   * the new corner, then let go of on the next frame so it slides the rest of
   * the way.
   */
  function land(landed: InsetSide, travel: number) {
    const from = side.value
    const at = (from === 'start' ? 0 : travel) + offset.value

    side.value = landed
    offset.value = at - (landed === 'start' ? 0 : travel)

    const release = () => {
      dragging.value = false
      offset.value = 0
    }

    if (typeof requestAnimationFrame === 'function') requestAnimationFrame(() => requestAnimationFrame(release))
    else setTimeout(release, 16)
  }

  return {
    side: readonly(side),
    style,
    reset,

    onPointerdown(event: PointerEvent) {
      event.stopPropagation()
      tookClick = false

      if (event.pointerType === 'mouse' && event.button !== 0) return

      const element = event.currentTarget as HTMLElement
      const frame = element.offsetParent as HTMLElement | null

      if (!frame) return

      press = { id: event.pointerId, x: event.clientX, y: event.clientY, travel: travelOf(element, frame.clientWidth, side.value) }
      moved = false
      element.setPointerCapture?.(event.pointerId)
    },

    onPointermove(event: PointerEvent) {
      if (!press || event.pointerId !== press.id) return

      event.stopPropagation()

      const dx = event.clientX - press.x

      if (!moved && Math.hypot(dx, event.clientY - press.y) < insetTapSlop) return

      moved = true
      dragging.value = true
      offset.value = clampDrag(side.value, dx, press.travel)
    },

    onPointerup(event: PointerEvent) {
      if (!press || event.pointerId !== press.id) return

      event.stopPropagation()

      const { travel, x } = press
      press = null

      if (!moved) return

      // It was a drag, so the click that lifting the finger makes is not a swap.
      tookClick = event.type === 'pointerup'
      land(event.type === 'pointerup' ? settleSide(side.value, event.clientX - x, travel) : side.value, travel)
    },

    onKeydown(event: KeyboardEvent) {
      const to = event.key === 'ArrowLeft' ? 'start' : event.key === 'ArrowRight' ? 'end' : null

      if (!to) return

      event.preventDefault()
      event.stopPropagation()
      side.value = to
      offset.value = 0
    },

    /** Whether the click that just arrived ended a drag, and should be ignored. Asking clears it. */
    tookClick(): boolean {
      const took = tookClick
      tookClick = false
      return took
    },
  }
}
