/**
 * Going back the way a person came (#118).
 *
 * A pushed screen's arrow used to be a link to a fixed address: a Do went
 * "back" to the Dos tab even when it had been opened from the start screen,
 * and because the link pushed a new entry, the system's back gesture then went
 * straight into the Do again. It steps back through the history instead — one
 * history for the whole app rather than one per tab, so Home → Do → chat, back,
 * back is Home again (docs/adr/0040-back-goes-where-you-came-from.md).
 *
 * `fallback` is where the screen sits when there is nothing in q2 to go back
 * to: it was opened from a notification, a link or a reload. That replaces the
 * entry rather than pushing one, so a later system back does not return to the
 * screen that was just left.
 */
export function useGoBack() {
  const router = useRouter()

  return function goBack(fallback: string) {
    // The router writes `back` itself on every navigation inside q2, and leaves
    // it null on the first entry q2 rendered. `history.state` is the router's
    // public view of it (RouterHistory.state).
    if (router.options.history.state.back) {
      router.back()
      return
    }

    void router.replace(fallback)
  }
}
