/**
 * Something a screen remembers about how it was left, handed back when that
 * screen is gone back to (#118) — how far it was scrolled, what was typed into
 * its search box, whether a sheet was open over it.
 *
 * It is kept in the history entry the screen was left from, next to what the
 * router keeps there itself, rather than in the address or in a store: it
 * belongs to that one visit. Going back finds the screen as it was, while the
 * same screen opened afresh from the tab bar starts clean — and a name somebody
 * typed into a search box never becomes part of an address, which travels into
 * share sheets, the browser's own history list and Sentry
 * (docs/adr/0040-back-goes-where-you-came-from.md).
 *
 * `capture` runs once, as the screen is left forwards, and never on every
 * keystroke: Safari throws after a hundred history writes in thirty seconds.
 * Nothing is written when the screen is left by going back, because by the time
 * the router asks, the browser has already moved to the earlier entry, and
 * writing then would put this screen's state on that one. A screen that is only
 * ever left backwards has nothing to come back to anyway.
 *
 * Returns what was remembered, or `undefined` on a fresh visit — and while the
 * server-rendered page is hydrated, because the server cannot know it and the
 * first render has to match the server's.
 */
export function useReturnState<T>(key: string, capture: () => T | undefined): T | undefined {
  if (!import.meta.client) return undefined

  const stateKey = `q2:${key}`
  const entry = window.history.state?.position
  const remembered = useNuxtApp().isHydrating ? undefined : window.history.state?.[stateKey] as T | undefined

  function remember() {
    // Still on this screen's own entry: the navigation is a push or a replace,
    // which the router performs only after this guard.
    if (window.history.state?.position !== entry) return

    window.history.replaceState({ ...window.history.state, [stateKey]: capture() }, '')
  }

  onBeforeRouteLeave(remember)

  // A query change keeps the screen but may replace its entry (the Dos tab's
  // segment); the router copies what is already there into the new one.
  onBeforeRouteUpdate(remember)

  return remembered
}
