import { hapticTap } from '~/utils/haptics'
import { PULL_SLOP, PULL_THRESHOLD, isPullArmed, isPullGesture, pullDistance } from '~/utils/pullToRefresh'

/**
 * Pulling a screen's list down to read it again (#19).
 *
 * Listens on the screen's one scroll area rather than the document, because
 * `main.css` turns the document's own overscroll off (and the iOS app has no
 * browser gesture to fall back on). Only a pull that starts with the list at
 * its top, and goes mostly downwards, counts; a sideways swipe — a proof card
 * on the vote stack — is left alone.
 *
 * Touch events rather than pointer events: the browser cancels a pointer the
 * moment it decides to scroll, and a pull at the top of a list is exactly the
 * movement it would like to scroll with. The move listener is not passive, so
 * a pull can stop the area from bouncing while the indicator follows it.
 *
 * Crossing the threshold gives one haptic tap (which respects reduced motion),
 * and letting go past it calls `refresh` — the one each screen's composable
 * already returns — holding the indicator until it answers. A failure is that
 * composable's to report, as it is for every other refresh.
 */
export function usePullToRefresh(area: () => HTMLElement | null, refresh: () => Promise<unknown>) {
  const distance = ref(0)
  const isRefreshing = ref(false)
  const isArmed = computed(() => isPullArmed(distance.value))

  let start: { x: number, y: number } | null = null
  let decided: boolean | null = null
  let tracked: HTMLElement | null = null

  function point(event: TouchEvent) {
    const touch = event.touches[0] ?? event.changedTouches[0]
    return touch ? { x: touch.clientX, y: touch.clientY } : null
  }

  function reset() {
    start = null
    decided = null
  }

  function onStart(event: TouchEvent) {
    const element = area()
    if (isRefreshing.value || !element || element.scrollTop > 0 || event.touches.length > 1) return

    start = point(event)
    decided = null
  }

  function onMove(event: TouchEvent) {
    const here = point(event)
    if (!start || !here) return

    const dx = here.x - start.x
    const dy = here.y - start.y

    if (decided === null) {
      if (Math.max(Math.abs(dx), Math.abs(dy)) < PULL_SLOP) return
      decided = isPullGesture(dx, dy)
    }

    if (!decided || (area()?.scrollTop ?? 0) > 0) {
      distance.value = 0
      reset()
      return
    }

    const wasArmed = isArmed.value
    distance.value = pullDistance(dy)

    if (!wasArmed && isArmed.value) hapticTap()
    if (event.cancelable) event.preventDefault()
  }

  async function onEnd() {
    const armed = decided === true && isArmed.value
    reset()

    if (!armed) {
      distance.value = 0
      return
    }

    isRefreshing.value = true
    distance.value = PULL_THRESHOLD

    try {
      await refresh()
    }
    catch {
      // The screen's composable reports its own failures; the indicator only
      // has to go away again.
    }
    finally {
      isRefreshing.value = false
      distance.value = 0
    }
  }

  function onCancel() {
    reset()
    distance.value = 0
  }

  onMounted(() => {
    tracked = area()
    if (!tracked) return

    tracked.addEventListener('touchstart', onStart, { passive: true })
    tracked.addEventListener('touchmove', onMove, { passive: false })
    tracked.addEventListener('touchend', onEnd)
    tracked.addEventListener('touchcancel', onCancel)
  })

  onBeforeUnmount(() => {
    tracked?.removeEventListener('touchstart', onStart)
    tracked?.removeEventListener('touchmove', onMove)
    tracked?.removeEventListener('touchend', onEnd)
    tracked?.removeEventListener('touchcancel', onCancel)
    tracked = null
  })

  return {
    distance: readonly(distance),
    isRefreshing: readonly(isRefreshing),
    isArmed,
  }
}
