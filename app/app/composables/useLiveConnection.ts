import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import type { Counts, NotificationLine } from '~/api/types'

/**
 * The parts of the screen the server can say have changed.
 *
 * A hand-written mirror of `LiveArea` in api/src/Q2.Api/Features/Notifications/
 * LiveHub.cs. The live connection is not a documented endpoint, so nothing
 * generates this; what travels over it — `Counts` above all — is generated.
 */
export type LiveArea = 'Notifications' | 'Chats' | 'Friends' | 'Proofs' | 'Goals' | 'Challenge' | 'Feed'

/** "This part of your screen changed — read it again." */
export interface LiveChange {
  area: LiveArea
  id: string | null
}

/** The three events the server sends. Mirrors `LiveEvents` on the server. */
export const liveEvents = { counts: 'counts', changed: 'changed', notification: 'notification' } as const

/**
 * How long a hidden page keeps its connection before letting it go.
 *
 * Long enough that switching to another app for a moment does not reconnect
 * twice; short enough that a phone put back in a pocket starts receiving
 * pushes again — the server skips the push for anybody who is connected.
 */
export const hiddenGraceMs = 10_000

/**
 * Which cached reads each part of the screen is made of.
 *
 * Refreshing a key nothing on screen uses does nothing at all, so this can
 * name every read a change touches and leave it to whichever is mounted. A
 * thread and a goal are named with their id, the way their own composables
 * key them.
 */
export function keysFor(change: LiveChange): string[] {
  switch (change.area) {
    case 'Chats':
      return change.id ? ['chats', `chat:${change.id}`] : ['chats']
    case 'Friends':
      return ['friends']
    case 'Proofs':
      return ['proofs-pending']
    case 'Goals':
      return change.id
        ? ['goals', 'goals-archive', 'home', `goal:${change.id}`]
        : ['goals', 'goals-archive', 'home']
    case 'Challenge':
      return ['challenge-today']
    case 'Feed':
      return ['home', 'activity-overview', 'profile']
    case 'Notifications':
    default:
      return ['notifications']
  }
}

/** The part of a SignalR connection this file needs — and what a test stands in for. */
export interface LiveTransport {
  readonly state: string
  start(): Promise<void>
  stop(): Promise<void>
  on(event: string, handler: (payload: never) => void): void
  onreconnected(handler: () => void): void
}

export interface LiveLinkOptions {
  connect: () => LiveTransport
  onCounts: (counts: Counts) => void
  onChanged: (change: LiveChange) => void

  /** Something that concerns this person, which the server has already decided may interrupt them. */
  onNotification: (line: NotificationLine) => void

  /** After a (re)connection: whatever moved in the meantime was not replayed. */
  onCaughtUp: () => void
}

/**
 * One connection, opened while it is wanted and closed when it is not.
 *
 * Kept apart from Nuxt so its rules can be tested on their own: nothing
 * connects until asked, nothing connects twice, and failing to connect is an
 * ordinary state — offline, signed out, a server restarting — rather than a
 * defect to report.
 */
export function createLiveLink(options: LiveLinkOptions) {
  let transport: LiveTransport | null = null

  function ensure(): LiveTransport {
    if (transport) return transport

    transport = options.connect()
    transport.on(liveEvents.counts, (counts: Counts) => options.onCounts(counts))
    transport.on(liveEvents.changed, (change: LiveChange) => options.onChanged(change))
    transport.on(liveEvents.notification, (line: NotificationLine) => options.onNotification(line))
    transport.onreconnected(() => options.onCaughtUp())

    return transport
  }

  async function open() {
    const current = ensure()

    if (current.state !== HubConnectionState.Disconnected) return

    try {
      await current.start()
      options.onCaughtUp()
    }
    catch {
      // Offline, signed out, or the server restarting. Not a defect and not
      // an issue: the page becoming visible again, or the next sign-in, tries
      // once more — and the badges are still read on every navigation.
    }
  }

  async function close() {
    if (!transport || transport.state === HubConnectionState.Disconnected) return

    try {
      await transport.stop()
    }
    catch {
      // A connection that is already going away needs no second opinion.
    }
  }

  return { open, close }
}

/**
 * The open app's line back from the server. Started once, in app.vue.
 *
 * It only ever listens: every change still goes through the REST API, so the
 * OpenAPI contract stays the only one (docs/adr/0024-one-notification-pipeline.md).
 * What arrives is one of three things:
 *
 * - **fresh badge numbers**, which replace the ones on screen — and when the
 *   bell's number went up, the bell reads itself again in case it is open;
 * - **"this part changed"**, which reads again whatever of that part is
 *   mounted (`keysFor`). The server says what changed, never what it now is,
 *   so every screen keeps reading through the endpoint that already applies
 *   every rule about who may see what;
 * - **a notification**, sent instead of a push because this person is
 *   looking, which becomes a banner at the top of the screen unless the screen
 *   already shows what it is about (`useNotificationToast`,
 *   docs/adr/0025-banners-in-the-open-app.md).
 *
 * Connected only while the page is visible and somebody is signed in. That is
 * more than thrift: the server skips the push for anybody connected, so
 * "connected" has to mean "looking" — a phone in a pocket must still ring.
 *
 * Client only. The server renders a page; it does not keep a line open for it.
 */
export function useLiveConnection() {
  if (import.meta.server) return

  const { public: config } = useRuntimeConfig()
  const { isSignedIn } = useSession()
  const counts = useNuxtData<Counts>('counts')
  const banner = useNotificationToast()

  const link = createLiveLink({
    connect: () => new HubConnectionBuilder()
      .withUrl(`${config.apiBaseUrl}/api/live`, { withCredentials: true })
      .withAutomaticReconnect()

      // Its own console output would describe every dropped connection on a
      // train as an error. What matters is already visible: the badges.
      .configureLogging(LogLevel.None)
      .build(),

    onCounts: (next) => {
      const previous = counts.data.value
      counts.data.value = next

      if (previous && next.unseenNotifications > previous.unseenNotifications) {
        void refreshNuxtData('notifications')
      }
    },

    onChanged: change => void refreshNuxtData(keysFor(change)),

    onNotification: line => banner.announce(line),

    // Changes while disconnected are not replayed. Refresh the mounted reads
    // as well as the badges so an open thread catches up after returning.
    onCaughtUp: () => void refreshNuxtData(),
  })

  let hidden: ReturnType<typeof setTimeout> | undefined

  function sync() {
    clearTimeout(hidden)

    if (!isSignedIn.value) {
      void link.close()
      return
    }

    if (document.visibilityState === 'visible') {
      void link.open()
      return
    }

    hidden = setTimeout(() => void link.close(), hiddenGraceMs)
  }

  onMounted(() => {
    document.addEventListener('visibilitychange', sync)
    watch(isSignedIn, sync, { immediate: true })
  })

  onBeforeUnmount(() => {
    document.removeEventListener('visibilitychange', sync)
    clearTimeout(hidden)
    void link.close()
  })
}
