/**
 * Pictures the iOS app has already fetched, kept as `blob:` addresses.
 *
 * An `<img>` in the app cannot send the bearer token, so every picture is
 * fetched with it and shown from memory (`useImageSource`). This is what keeps
 * that from happening twice: an avatar appears on half the screens in q2, and
 * the browser's own cache — which the web build relies on — never sees these
 * requests.
 *
 * Bounded, oldest out first. A long scroll through a year of proof photographs
 * would otherwise keep every one of them in memory until the app is closed.
 * An evicted address is revoked; an `<img>` that already painted it keeps its
 * picture, and one that asks again fetches it again.
 */
export interface ObjectUrlCache {
  /** The address for `id`, loading it with `load` the first time. */
  get: (id: string, load: () => Promise<Blob>) => Promise<string>
  /** Forgets everything — on sign-out, so nobody else's pictures stay behind. */
  clear: () => void
}

export interface ObjectUrlCacheOptions {
  capacity?: number
  create?: (blob: Blob) => string
  revoke?: (url: string) => void
}

export function createObjectUrlCache({
  capacity = 100,
  create = blob => URL.createObjectURL(blob),
  revoke = url => URL.revokeObjectURL(url),
}: ObjectUrlCacheOptions = {}): ObjectUrlCache {
  // A Map iterates in insertion order, so re-inserting on every hit makes the
  // first key the least recently used.
  const entries = new Map<string, Promise<string>>()

  function release(entry: Promise<string>) {
    entry.then(revoke, () => undefined)
  }

  function get(id: string, load: () => Promise<Blob>): Promise<string> {
    const cached = entries.get(id)

    if (cached) {
      entries.delete(id)
      entries.set(id, cached)
      return cached
    }

    const entry = load().then(create)
    entries.set(id, entry)

    // A failure is not remembered: the next screen that shows this picture
    // tries again, the way an `<img>` would.
    entry.catch(() => {
      if (entries.get(id) === entry) entries.delete(id)
    })

    while (entries.size > capacity) {
      const [oldest, evicted] = entries.entries().next().value!
      entries.delete(oldest)
      release(evicted)
    }

    return entry
  }

  function clear() {
    for (const entry of entries.values()) release(entry)
    entries.clear()
  }

  return { get, clear }
}
