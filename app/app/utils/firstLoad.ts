/**
 * What a screen shows before its first answer, and how to tell it apart.
 *
 * `status === 'pending'` means two different things to a screen: its first
 * read, when there is nothing to draw but a skeleton, and every read after it —
 * including the ones the live connection starts in the background while
 * somebody is using the screen (useLiveConnection). Drawing the skeleton for
 * the second kind swaps the screen out from under them, a thread's composer
 * included, with what they were typing in it.
 *
 * So a read's default value is marked as a placeholder, and a screen is loading
 * only while it still shows one. After the first answer it keeps what it has
 * until the next answer replaces it. A new key — another thread, another goal —
 * starts from a fresh placeholder, and loads like the first read it is.
 */
const initialPayload = Symbol('initialPayload')

/** Marks a `useAsyncData` default as "nothing read yet". */
export function placeholder<T extends object>(value: T): T {
  // A non-enumerable marker stays with this payload, without a shared registry
  // or a field copied into real responses by an object spread.
  Object.defineProperty(value, initialPayload, { value: true })
  return value
}

/** Whether a read is pending with nothing to show yet: a first read, not a refresh. */
export function isFirstLoad(status: string, data: unknown): boolean {
  return status === 'pending'
    && typeof data === 'object'
    && data !== null
    && initialPayload in data
}
