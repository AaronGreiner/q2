/**
 * One shared "now" for everything that shows a relative time.
 *
 * Feeds and chats are full of "vor 12 Min". Reading the clock per component
 * would give the server one answer and the browser another a second later,
 * which is a hydration mismatch on every screen that has a timestamp on it.
 * `useState` is serialised into the SSR payload, so the client hydrates against
 * the exact instant the server rendered with, and only then starts moving.
 */
let isTicking = false

export function useNow(): Ref<number> {
  const now = useState('q2:now', () => Date.now())

  if (import.meta.client && !isTicking) {
    isTicking = true

    // Once a minute — the smallest unit anything here displays. It runs for the
    // lifetime of the application and there is nothing to clean up: the page
    // going away takes the timer with it, and stopping it when the first
    // component that asked happens to unmount would freeze every clock on the
    // next screen.
    setInterval(() => {
      now.value = Date.now()
    }, 60_000)
  }

  return now
}

/**
 * How far the reader's clock is ahead of UTC, in minutes.
 *
 * Zero on the server and during the first client render, then corrected once
 * the component is mounted. That order is the whole point: the Nitro server has
 * no idea which time zone the reader is in, so anything it renders would
 * disagree with the browser and produce a hydration mismatch on every screen
 * with a timestamp. Correcting *after* hydration is an ordinary reactive
 * update instead.
 *
 * q2 stores instants in UTC and has no notion of a person's time zone in the
 * model — there are no accounts to hang one on — so this is the browser's own
 * offset, not a stored preference. See docs/next-steps.md.
 */
export function useTimeZoneOffset(): Ref<number> {
  const offset = useState('q2:zone-offset', () => 0)

  onMounted(() => {
    offset.value = -new Date().getTimezoneOffset()
  })

  return offset
}
