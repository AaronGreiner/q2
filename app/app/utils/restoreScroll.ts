const interruptions = ['wheel', 'touchstart', 'pointerdown', 'keydown'] as const

/**
 * Scrolls `element` down to `top` as soon as its content is tall enough to get
 * there (#118).
 *
 * A screen that is gone back to is rendered again and fetches its data again,
 * so at first it is a skeleton too short to scroll anywhere. Every frame checks
 * again, for at most `patience` milliseconds. When the list came back shorter
 * than it was, it ends up as far down as it now goes.
 *
 * The attempt is abandoned the moment the person touches, scrolls or types:
 * they have started using the screen, and moving it under their finger would
 * be worse than not restoring at all.
 *
 * Returns a function that abandons it, for a screen left before it finished.
 */
export function restoreScroll(element: HTMLElement, top: number, patience = 1500): () => void {
  const deadline = performance.now() + patience
  let frame = 0

  function stop() {
    cancelAnimationFrame(frame)
    for (const type of interruptions) element.removeEventListener(type, stop)
  }

  function attempt() {
    const reachable = element.scrollHeight - element.clientHeight >= top

    if (reachable || performance.now() >= deadline) {
      element.scrollTop = top
      stop()
      return
    }

    frame = requestAnimationFrame(attempt)
  }

  for (const type of interruptions) element.addEventListener(type, stop, { passive: true })
  attempt()

  return stop
}
