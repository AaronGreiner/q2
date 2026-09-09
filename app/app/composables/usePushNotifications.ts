/**
 * Turning notifications on for *this* device.
 *
 * Per device rather than per account, because that is what a push subscription
 * is: the same person on a phone and a laptop is two of them, and both should
 * ring. Which is also why none of this is a stored preference — the switch
 * reflects what this browser has actually agreed to, not what the account
 * wishes it had.
 *
 * Four states worth telling apart, because the remedy differs for each:
 *
 * - **unsupported** — this browser has no push at all. Nothing to offer.
 * - **unavailable** — this deployment has no VAPID keys. Not the person's
 *   problem and not fixable from here.
 * - **blocked** — the browser refused, permanently. Only the browser's own
 *   settings can undo that, and saying so is the only useful thing left.
 * - **off / on** — the switch does something.
 */
export type PushState = 'unsupported' | 'unavailable' | 'blocked' | 'off' | 'on'

export function usePushNotifications() {
  const api = useQ2Api()
  const { report } = useErrorReporter()

  const state = ref<PushState>('unsupported')
  const isBusy = ref(false)

  /**
   * Whether the browser can do any of this.
   *
   * Checked rather than assumed: iOS only gained push in a home-screen app,
   * and a desktop browser in a private window has the API but refuses to
   * register anything.
   */
  function isSupported(): boolean {
    return import.meta.client
      && 'serviceWorker' in navigator
      && 'PushManager' in window
      && 'Notification' in window
  }

  async function subscription(): Promise<globalThis.PushSubscription | null> {
    if (!isSupported()) return null

    const registration = await navigator.serviceWorker.ready
    return await registration.pushManager.getSubscription()
  }

  /** Works out where this device stands, without asking for anything. */
  async function resolve() {
    if (!isSupported()) {
      state.value = 'unsupported'
      return
    }

    try {
      if (!(await api.notifications.key()).isAvailable) {
        state.value = 'unavailable'
        return
      }
    }
    catch (caught) {
      report(caught, { feature: 'notifications', action: 'key' })
      state.value = 'unavailable'
      return
    }

    if (Notification.permission === 'denied') {
      state.value = 'blocked'
      return
    }

    state.value = await subscription() === null ? 'off' : 'on'
  }

  /**
   * Asks the browser, then registers what it hands back.
   *
   * The permission prompt is only ever raised from here — that is, from a tap
   * on a switch somebody just read the label of. A prompt on page load is the
   * fastest way to a permanently blocked browser, and blocked cannot be undone
   * by the app.
   */
  async function enable() {
    if (isBusy.value || !isSupported()) return

    isBusy.value = true

    try {
      const key = await api.notifications.key()

      if (!key.isAvailable || !key.publicKey) {
        state.value = 'unavailable'
        return
      }

      if (await Notification.requestPermission() !== 'granted') {
        state.value = Notification.permission === 'denied' ? 'blocked' : 'off'
        return
      }

      const registration = await navigator.serviceWorker.ready

      const created = await registration.pushManager.subscribe({
        // Required by every browser: a subscription that anybody could send to
        // is one anybody could send to.
        userVisibleOnly: true,
        applicationServerKey: decodeKey(key.publicKey),
      })

      const json = created.toJSON()

      await api.notifications.subscribe(
        created.endpoint,
        json.keys?.p256dh ?? '',
        json.keys?.auth ?? '',
      )

      state.value = 'on'
    }
    catch (caught) {
      report(caught, { feature: 'notifications', action: 'enable' })
      await resolve()
    }
    finally {
      isBusy.value = false
    }
  }

  /**
   * Both halves, in this order.
   *
   * The browser's subscription goes first: if the server were told first and
   * the browser then refused to unsubscribe, the device would keep receiving
   * pushes the server no longer knows it is sending. The other way round leaves
   * at worst a row that the next failed delivery removes on its own.
   */
  async function disable() {
    if (isBusy.value) return

    isBusy.value = true

    try {
      const existing = await subscription()

      if (existing) {
        await existing.unsubscribe()
        await api.notifications.unsubscribe(existing.endpoint)
      }

      state.value = 'off'
    }
    catch (caught) {
      report(caught, { feature: 'notifications', action: 'disable' })
      await resolve()
    }
    finally {
      isBusy.value = false
    }
  }

  return {
    state: computed(() => state.value),
    isBusy: computed(() => isBusy.value),
    resolve,
    enable,
    disable,
  }
}

/**
 * The VAPID public key, as `pushManager.subscribe` wants it.
 *
 * base64url in, raw bytes out. The browser will not take the string, and the
 * padding it lacks is what `atob` insists on.
 */
function decodeKey(base64Url: string): Uint8Array<ArrayBuffer> {
  const padded = base64Url.replaceAll('-', '+').replaceAll('_', '/')
    .padEnd(base64Url.length + ((4 - (base64Url.length % 4)) % 4), '=')

  const binary = atob(padded)

  // Backed by a plain ArrayBuffer, which `applicationServerKey` insists on: a
  // `Uint8Array` whose buffer might be shared is not a `BufferSource`.
  const bytes = new Uint8Array(new ArrayBuffer(binary.length))

  for (let index = 0; index < binary.length; index++) {
    bytes[index] = binary.charCodeAt(index)
  }

  return bytes
}
