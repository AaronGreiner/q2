import type { Settings } from '~/api/types'

/**
 * Whether the signed-in person sees others' activity — the online dot and
 * "vor 9 Min aktiv" (#106).
 *
 * The app's half of a deal the server cannot keep alone: somebody who hides
 * their own activity does not see anybody else's. The server already withholds
 * the activity of whoever hides it, from every response; this hides the rest
 * from whoever hid theirs.
 *
 * Read from the settings the app has already loaded (`useAppSettings`, under
 * the `settings` key) rather than through it, because every avatar asks, and
 * `useAppSettings` would add a watcher per avatar. Until they have arrived
 * the answer is yes, which is everybody's default.
 */
export function useSeesActivity() {
  const { data } = useNuxtData<Settings | null>('settings')
  return computed(() => data.value?.showActivity !== false)
}
