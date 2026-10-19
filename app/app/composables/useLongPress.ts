import { hapticTap } from '~/utils/haptics'

/** How long a finger has to stay before it is a long press rather than a tap. */
const holdMs = 450

/** Movement beyond this is a scroll, not a press. */
const slop = 10

/**
 * Pressing and holding something, the way a phone opens a message's menu (#25).
 *
 * A held finger opens it after `holdMs`, with one haptic tap; moving more than
 * a few pixels is a scroll and cancels it. A right click does the same on a
 * desktop, and Enter or the context-menu key on a keyboard, so the menu never
 * depends on a gesture alone (the same rule the swipe vote follows).
 *
 * The click that ends a long press is swallowed: the finger lifts on whatever
 * it held, often a photograph, and that photograph must not open as well.
 */
export function useLongPress(onPress: () => void) {
  let timer: ReturnType<typeof setTimeout> | null = null
  let start: { x: number, y: number } | null = null
  let fired = false

  function cancel() {
    if (timer) clearTimeout(timer)
    timer = null
    start = null
  }

  function press() {
    fired = true
    cancel()
    hapticTap()
    onPress()
  }

  onBeforeUnmount(cancel)

  return {
    onPointerdown(event: PointerEvent) {
      fired = false
      if (event.pointerType === 'mouse') return

      start = { x: event.clientX, y: event.clientY }
      timer = setTimeout(press, holdMs)
    },

    onPointermove(event: PointerEvent) {
      if (start && Math.hypot(event.clientX - start.x, event.clientY - start.y) > slop) cancel()
    },

    onPointerup: cancel,
    onPointercancel: cancel,

    onContextmenu(event: MouseEvent) {
      event.preventDefault()
      if (!fired) press()
    },

    onKeydown(event: KeyboardEvent) {
      if (event.key !== 'Enter' && event.key !== 'ContextMenu') return

      event.preventDefault()
      press()
    },

    /** Bound with `.capture`, so it runs before the photograph's own click. */
    onClickCapture(event: MouseEvent) {
      if (!fired) return

      event.preventDefault()
      event.stopPropagation()
      fired = false
    },
  }
}
