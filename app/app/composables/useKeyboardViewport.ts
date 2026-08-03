/**
 * Keeps the app shell inside the part of the screen the keyboard leaves over.
 *
 * A native app is told the keyboard is coming and lays itself out again. A web
 * view is not, so WebKit falls back to the only lever it has: it scrolls the
 * page until the focused field is visible. The shell is exactly one viewport
 * tall and has nothing to scroll inside it, so the whole app goes up instead —
 * on a phone with a translucent-free status bar that means the chat header
 * slides up behind the clock and stays there until the field is blurred. It
 * looks broken because it is not a layout at all, it is a scroll.
 *
 * The visual viewport does know where the keyboard is: while it covers part of
 * the screen, `visualViewport.height` is the space actually left. Pinning the
 * shell to that puts the composer to rest directly on top of the keyboard, the
 * way it comes to rest in a native app, and leaves WebKit nothing to scroll.
 *
 * Two properties come out of this, because there are two kinds of thing to fix:
 *
 *   --q2-viewport-height  what is left of the screen. The layouts size the
 *                         shell with it.
 *   --q2-keyboard-inset   how much is covered. Anything that is teleported out
 *                         of the shell and pinned to the viewport — every
 *                         overlay, since they are fixed children of <body> and
 *                         so are not inside the shell at all — is lifted by it.
 *
 * Both are set only while the keyboard is up. The rest of the time the shells
 * fall back to 100dvh, which is what the server renders and what the first
 * frame is laid out with — and that first frame is delicate on iOS for reasons
 * app.vue goes into, so nothing here may touch it.
 */
export function useKeyboardViewport(): void {
  if (import.meta.server) return

  let viewport: VisualViewport | undefined

  function apply(): void {
    if (!viewport) return

    /*
     * window.innerHeight does not move when the keyboard opens on iOS, so the
     * difference between the two is the strip the keyboard covers. A fraction
     * of a pixel of it is rounding on a zoomed-out page, not a keyboard.
     */
    const covered = window.innerHeight - viewport.height

    if (covered > 1) {
      document.documentElement.style.setProperty('--q2-viewport-height', `${viewport.height}px`)
      document.documentElement.style.setProperty('--q2-keyboard-inset', `${covered}px`)

      // WebKit may have started scrolling to reveal the field before the shell
      // shrank. It fits now, so put the app back against the top of the screen.
      window.scrollTo(0, 0)
    }
    else {
      document.documentElement.style.removeProperty('--q2-viewport-height')
      document.documentElement.style.removeProperty('--q2-keyboard-inset')
    }
  }

  onMounted(() => {
    viewport = window.visualViewport ?? undefined
    viewport?.addEventListener('resize', apply)
    apply()
  })

  onUnmounted(() => {
    viewport?.removeEventListener('resize', apply)
    document.documentElement.style.removeProperty('--q2-viewport-height')
    document.documentElement.style.removeProperty('--q2-keyboard-inset')
  })
}
