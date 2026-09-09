import { computed, nextTick, ref, watch } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { de } from '~/i18n/messages'
import { useChallengeArchive, useChallengeRoom } from '~/composables/useChallenge'
import { useInvite } from '~/composables/useInvite'
import { usePendingInvite } from '~/composables/usePendingInvite'
import { usePushNotifications } from '~/composables/usePushNotifications'
import { usePersonProfile, useProfile } from '~/composables/useFriends'
import { useActivityOverview } from '~/composables/useHome'
import { usePendingProofs, useProofDelivery } from '~/composables/useProofs'
import { useBlockedPeople, useSafety } from '~/composables/useSafety'

/**
 * The composables behind stages 7 to 9.
 *
 * They hold no rules of their own — who may see a challenge room, what a block
 * reaches and who gets a notification are all decided on the server, and there
 * are tests for every one of those over there. What is left here is the part
 * that *is* this layer's job and cannot be checked anywhere else: that a
 * failure never escapes as an exception, that the whole payload is replaced
 * rather than mutated (a shallow ref changed in place moves nothing on screen),
 * and that a browser which refuses is a state rather than a defect.
 *
 * The harness is the one `dataComposables.spec.ts` established: real Vue
 * reactivity, a faithful stand-in for Nuxt's shallow async-data contract, and
 * the feature globals stubbed.
 */
const failure = {
  kind: 'network' as const,
  isExpected: true,
  status: null,
  fieldErrors: {},
  traceId: null,
  errorId: null,
  reason: null,
}

function person(overrides: Record<string, unknown> = {}) {
  return { id: 'person-1', displayName: 'Jonas', handle: '@jonas', ...overrides }
}

function entry(overrides: Record<string, unknown> = {}) {
  return {
    id: 'entry-1',
    author: person(),
    imageId: 'image-1',
    capturedInApp: true,
    createdAt: '2026-09-09T09:00:00Z',
    isMine: false,
    reactions: [],
    ...overrides,
  }
}

function room(overrides: Record<string, unknown> = {}) {
  return {
    challenge: {
      id: 'challenge-1',
      prompt: 'Zeig deinen Arbeitsplatz.',
      publishedAt: '2026-09-09T00:00:00Z',
      expiresAt: '2026-09-10T00:00:00Z',
    },
    ownEntry: null,
    entries: [],
    friendCount: 3,
    isRevealed: false,
    ...overrides,
  }
}

function installReactiveGlobals() {
  vi.stubGlobal('ref', ref)
  vi.stubGlobal('computed', computed)
  vi.stubGlobal('watch', watch)
}

/** The same faithful stand-in for Nuxt's shallow async-data contract. */
function installAsyncData() {
  vi.stubGlobal('useAsyncData', (
    _key: unknown,
    handler: () => Promise<unknown>,
    options: { default?: () => unknown } = {},
  ) => {
    const data = ref(options.default?.())
    const status = ref('pending')
    const refresh = vi.fn(async () => {
      status.value = 'pending'
      data.value = await handler()
      status.value = 'success'
      return data.value
    })

    void refresh()
    return { data, status, refresh }
  })
}

function installFeatureGlobals(api: object) {
  const report = vi.fn(() => failure)
  const show = vi.fn()

  vi.stubGlobal('useQ2Api', () => api)
  vi.stubGlobal('useErrorReporter', () => ({ report }))
  vi.stubGlobal('useToastMessage', () => ({ show }))
  vi.stubGlobal('useMessages', () => ref(de))

  return { report, show }
}

beforeEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  installReactiveGlobals()
  installAsyncData()
})

describe('useChallengeRoom', () => {
  function challengeApi(overrides: Record<string, unknown> = {}) {
    return {
      challenges: {
        today: vi.fn().mockResolvedValue({ room: room() }),
        submit: vi.fn().mockResolvedValue(room({ ownEntry: entry({ isMine: true }), isRevealed: true })),
        withdraw: vi.fn().mockResolvedValue({ room: room() }),
        react: vi.fn().mockResolvedValue(entry({ reactions: [{ kind: 'Fire', count: 1, isMine: true }] })),
        archive: vi.fn().mockResolvedValue([]),
        ...overrides,
      },
    }
  }

  it('loads today and reports nothing when there is nothing running', async () => {
    const api = challengeApi()
    api.challenges.today.mockResolvedValue({ room: null })
    installFeatureGlobals(api)

    const state = useChallengeRoom()

    await vi.waitFor(() => expect(state.isLoading.value).toBe(false))
    expect(state.room.value).toBeNull()
    expect(state.error.value).toBeNull()
  })

  /**
   * Contributing changes the room in a way no client could guess: friends'
   * pictures that were not in the previous response arrive with it, because
   * contributing is what earns them. So the answer replaces the payload.
   */
  it('replaces the whole room with what the server sent back', async () => {
    const api = challengeApi()
    const { show } = installFeatureGlobals(api)
    const state = useChallengeRoom()

    await vi.waitFor(() => expect(state.room.value).not.toBeNull())
    expect(state.room.value?.isRevealed).toBe(false)

    await state.contribute({ id: 'image-2' } as never)

    expect(api.challenges.submit).toHaveBeenCalledWith('image-2', true)
    expect(state.room.value?.isRevealed).toBe(true)
    expect(state.room.value?.ownEntry).not.toBeNull()
    expect(show).toHaveBeenCalledWith(de.toast.challengeJoined)
  })

  it('withdrawing covers the room again', async () => {
    const api = challengeApi()
    const { show } = installFeatureGlobals(api)
    const state = useChallengeRoom()

    await vi.waitFor(() => expect(state.room.value).not.toBeNull())
    await state.withdraw()

    expect(state.room.value?.ownEntry).toBeNull()
    expect(show).toHaveBeenCalledWith(de.toast.challengeWithdrawn)
  })

  it('puts an updated contribution in place without mutating the old one', async () => {
    const api = challengeApi()
    api.challenges.today.mockResolvedValue({
      room: room({ ownEntry: entry({ id: 'mine', isMine: true }), entries: [entry()], isRevealed: true }),
    })
    installFeatureGlobals(api)

    const state = useChallengeRoom()
    await vi.waitFor(() => expect(state.room.value?.entries).toHaveLength(1))

    const before = state.room.value
    await state.react('entry-1', 'Fire')

    expect(state.room.value).not.toBe(before)
    expect(state.room.value?.entries[0]?.reactions).toHaveLength(1)
  })

  it('reacts on your own contribution too', async () => {
    const api = challengeApi()
    api.challenges.react.mockResolvedValue(entry({ id: 'mine', isMine: true, reactions: [] }))
    api.challenges.today.mockResolvedValue({
      room: room({ ownEntry: entry({ id: 'mine', isMine: true }), isRevealed: true }),
    })
    installFeatureGlobals(api)

    const state = useChallengeRoom()
    await vi.waitFor(() => expect(state.room.value?.ownEntry).not.toBeNull())

    await state.react('mine', 'Applause')
    expect(state.room.value?.ownEntry?.id).toBe('mine')
  })

  it('does nothing at all when there is no room to react in', async () => {
    const api = challengeApi()
    api.challenges.today.mockResolvedValue({ room: null })
    installFeatureGlobals(api)

    const state = useChallengeRoom()
    await vi.waitFor(() => expect(state.isLoading.value).toBe(false))

    await state.react('entry-1', 'Fire')
    expect(state.room.value).toBeNull()
  })

  /**
   * Every failure is reported and swallowed. A composable that threw would
   * leave a screen with an unhandled rejection and no error state.
   */
  it('turns every failure into a reported one', async () => {
    const api = challengeApi()
    api.challenges.today.mockRejectedValueOnce(new Error('offline'))
    const { report } = installFeatureGlobals(api)

    const state = useChallengeRoom()
    await vi.waitFor(() => expect(state.error.value).toEqual(failure))
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'challenge', action: 'today' })

    api.challenges.submit.mockRejectedValueOnce(new Error('gone'))
    await state.contribute({ id: 'image-2' } as never)
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'challenge', action: 'contribute' })

    api.challenges.withdraw.mockRejectedValueOnce(new Error('gone'))
    await state.withdraw()
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'challenge', action: 'withdraw' })

    api.challenges.react.mockRejectedValueOnce(new Error('gone'))
    await state.react('entry-1', 'Fire')
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'challenge', action: 'react' })
  })

  it('refuses to send two contributions at once', async () => {
    const api = challengeApi()
    let release: (value: unknown) => void = () => {}
    api.challenges.submit.mockImplementation(() => new Promise((resolve) => {
      release = resolve
    }))
    installFeatureGlobals(api)

    const state = useChallengeRoom()
    await vi.waitFor(() => expect(state.room.value).not.toBeNull())

    const first = state.contribute({ id: 'image-2' } as never)
    await nextTick()
    expect(state.isSubmitting.value).toBe(true)

    await state.contribute({ id: 'image-3' } as never)
    expect(api.challenges.submit).toHaveBeenCalledTimes(1)

    release(room())
    await first

    // And the same guard on the way out.
    const withdrawing = state.withdraw()
    await state.withdraw()
    await withdrawing
  })

  it('loads the archive and reports its failure', async () => {
    const api = challengeApi()
    api.challenges.archive.mockResolvedValue([{ challenge: room().challenge, entry: entry({ isMine: true }) }])
    installFeatureGlobals(api)

    const archive = useChallengeArchive()
    await vi.waitFor(() => expect(archive.entries.value).toHaveLength(1))
    expect(archive.error.value).toBeNull()

    api.challenges.archive.mockRejectedValueOnce(new Error('offline'))
    await archive.refresh()
    expect(archive.error.value).toEqual(failure)
  })
})

describe('useSafety', () => {
  function moderationApi() {
    return {
      moderation: {
        report: vi.fn().mockResolvedValue({ id: 'report-1', createdAt: '2026-09-09T09:00:00Z' }),
        blocked: vi.fn().mockResolvedValue([person()]),
        block: vi.fn().mockResolvedValue([person()]),
        unblock: vi.fn().mockResolvedValue([]),
      },
    }
  }

  it('files a report and thanks once', async () => {
    const api = moderationApi()
    const { show } = installFeatureGlobals(api)
    const safety = useSafety()

    expect(await safety.report('Person', 'person-1', 'Spam', 'kurz')).toBe(true)
    expect(api.moderation.report).toHaveBeenCalledWith('Person', 'person-1', 'Spam', 'kurz')
    expect(show).toHaveBeenCalledWith(de.toast.reported)
  })

  it('blocks somebody and says so once', async () => {
    const api = moderationApi()
    const { show } = installFeatureGlobals(api)
    const safety = useSafety()

    expect(await safety.block('person-1')).toBe(true)
    expect(show).toHaveBeenCalledWith(de.toast.blocked)
  })

  it('answers false rather than throwing when either is refused', async () => {
    const api = moderationApi()
    api.moderation.report.mockRejectedValueOnce(new Error('gone'))
    api.moderation.block.mockRejectedValueOnce(new Error('gone'))
    const { report } = installFeatureGlobals(api)
    const safety = useSafety()

    expect(await safety.report('Proof', 'proof-1', 'Faked', '')).toBe(false)
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'moderation', action: 'report' })

    expect(await safety.block('person-1')).toBe(false)
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'moderation', action: 'block' })
  })

  it('does one thing at a time', async () => {
    const api = moderationApi()
    let release: (value: unknown) => void = () => {}
    api.moderation.block.mockImplementation(() => new Promise((resolve) => {
      release = resolve
    }))
    installFeatureGlobals(api)

    const safety = useSafety()
    const first = safety.block('person-1')
    await nextTick()

    expect(safety.isBusy.value).toBe(true)
    expect(await safety.report('Person', 'person-2', 'Spam', '')).toBe(false)

    release([])
    await first
    expect(safety.isBusy.value).toBe(false)
  })

  /**
   * The server answers with the list, so the screen is drawn from what it says
   * rather than from what the client guessed.
   */
  it('draws the blocked list from the answer, and reloads when unblocking fails', async () => {
    const api = moderationApi()
    const { show } = installFeatureGlobals(api)
    const blocked = useBlockedPeople()

    await vi.waitFor(() => expect(blocked.people.value).toHaveLength(1))

    await blocked.unblock('person-1')
    expect(blocked.people.value).toHaveLength(0)
    expect(show).toHaveBeenCalledWith(de.toast.unblocked)

    api.moderation.unblock.mockRejectedValueOnce(new Error('gone'))
    api.moderation.blocked.mockResolvedValueOnce([person()])
    await blocked.unblock('person-1')

    expect(blocked.people.value).toHaveLength(1)
  })

  it('reports a list that cannot be loaded', async () => {
    const api = moderationApi()
    api.moderation.blocked.mockRejectedValueOnce(new Error('offline'))
    const { report } = installFeatureGlobals(api)

    const blocked = useBlockedPeople()
    await vi.waitFor(() => expect(blocked.error.value).toEqual(failure))
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'moderation', action: 'blocked' })
    expect(blocked.people.value).toEqual([])
  })

  it('unblocks one at a time', async () => {
    const api = moderationApi()
    let release: (value: unknown) => void = () => {}
    api.moderation.unblock.mockImplementation(() => new Promise((resolve) => {
      release = resolve
    }))
    installFeatureGlobals(api)

    const blocked = useBlockedPeople()
    await vi.waitFor(() => expect(blocked.people.value).toHaveLength(1))

    const first = blocked.unblock('person-1')
    await nextTick()
    expect(blocked.isUnblocking.value).toBe(true)

    await blocked.unblock('person-1')
    expect(api.moderation.unblock).toHaveBeenCalledTimes(1)

    release([])
    await first
  })
})

describe('useInvite', () => {
  function inviteApi() {
    return {
      invite: {
        get: vi.fn().mockResolvedValue({ code: 'abc123' }),
        regenerate: vi.fn().mockResolvedValue({ code: 'def456' }),
      },
    }
  }

  function installBrowser(overrides: Record<string, unknown> = {}) {
    vi.stubGlobal('window', { location: { origin: 'https://q2.example' } })
    vi.stubGlobal('navigator', { clipboard: { writeText: vi.fn().mockResolvedValue(undefined) }, ...overrides })
  }

  /**
   * The server hands back a code and nothing else: it does not know which host
   * the app is served from, and a link with the wrong origin in it is worse
   * than no link.
   */
  it('builds the link from the page it is on', async () => {
    installBrowser()
    installFeatureGlobals(inviteApi())

    const invite = useInvite()
    await vi.waitFor(() => expect(invite.code.value).toBe('abc123'))

    expect(invite.url.value).toBe('https://q2.example/join/abc123')
  })

  it('replaces the code, and the link with it', async () => {
    installBrowser()
    const api = inviteApi()
    installFeatureGlobals(api)

    const invite = useInvite()
    await vi.waitFor(() => expect(invite.code.value).toBe('abc123'))

    await invite.replace()
    expect(invite.url.value).toBe('https://q2.example/join/def456')
  })

  it('copies to the clipboard when there is no share sheet', async () => {
    installBrowser()
    const { show } = installFeatureGlobals(inviteApi())

    const invite = useInvite()
    await vi.waitFor(() => expect(invite.url.value).not.toBe(''))

    await invite.share()

    expect(navigator.clipboard.writeText).toHaveBeenCalledWith('https://q2.example/join/abc123')
    expect(show).toHaveBeenCalledWith(de.toast.inviteCopied)
  })

  it('prefers the share sheet where there is one', async () => {
    const share = vi.fn().mockResolvedValue(undefined)
    installBrowser({ share })
    const { show } = installFeatureGlobals(inviteApi())

    const invite = useInvite()
    await vi.waitFor(() => expect(invite.url.value).not.toBe(''))

    await invite.share()

    expect(share).toHaveBeenCalledWith(expect.objectContaining({ url: 'https://q2.example/join/abc123' }))

    // A share sheet is its own confirmation; a second one would be noise.
    expect(show).not.toHaveBeenCalled()
  })

  /**
   * A dismissed share sheet and a clipboard the browser refused both land in
   * the same place, and neither is a defect worth an issue.
   */
  it('says nothing when the device refuses', async () => {
    installBrowser({ share: vi.fn().mockRejectedValue(new Error('dismissed')) })
    const { report, show } = installFeatureGlobals(inviteApi())

    const invite = useInvite()
    await vi.waitFor(() => expect(invite.url.value).not.toBe(''))

    await invite.share()

    expect(report).not.toHaveBeenCalled()
    expect(show).not.toHaveBeenCalled()
  })

  it('has nothing to share before the code arrives', async () => {
    installBrowser()
    const api = inviteApi()
    api.invite.get.mockRejectedValueOnce(new Error('offline'))
    const { report } = installFeatureGlobals(api)

    const invite = useInvite()
    await vi.waitFor(() => expect(invite.error.value).toEqual(failure))

    expect(invite.url.value).toBe('')
    await invite.share()
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'invite', action: 'get' })
  })

  it('reloads when replacing fails, and replaces one at a time', async () => {
    installBrowser()
    const api = inviteApi()
    installFeatureGlobals(api)

    const invite = useInvite()
    await vi.waitFor(() => expect(invite.code.value).toBe('abc123'))

    api.invite.regenerate.mockRejectedValueOnce(new Error('gone'))
    await invite.replace()
    expect(invite.code.value).toBe('abc123')

    let release: (value: unknown) => void = () => {}
    api.invite.regenerate.mockImplementation(() => new Promise((resolve) => {
      release = resolve
    }))

    const first = invite.replace()
    await nextTick()
    expect(invite.isReplacing.value).toBe(true)

    await invite.replace()
    release({ code: 'ghi789' })
    await first
  })
})

describe('usePendingInvite', () => {
  it('is one piece of state, shared by the link and the form', () => {
    const store = new Map<string, unknown>()

    vi.stubGlobal('useState', (key: string, init: () => unknown) => {
      if (!store.has(key)) store.set(key, ref(init()))
      return store.get(key)
    })

    const fromLink = usePendingInvite()
    fromLink.value = 'abc123'

    // Not a cookie and not local storage: a code that outlived the visit would
    // sit in the browser of somebody who decided not to sign up.
    expect(usePendingInvite().value).toBe('abc123')
  })
})

describe('usePushNotifications', () => {
  function pushApi(available = true) {
    return {
      notifications: {
        key: vi.fn().mockResolvedValue({ isAvailable: available, publicKey: available ? 'BObo0Ie7wLSM' : null }),
        subscribe: vi.fn().mockResolvedValue(undefined),
        unsubscribe: vi.fn().mockResolvedValue(undefined),
      },
    }
  }

  function installBrowser(options: {
    permission?: NotificationPermission
    subscription?: unknown
    requestPermission?: () => Promise<NotificationPermission>
    subscribe?: () => Promise<unknown>
  } = {}) {
    const pushManager = {
      getSubscription: vi.fn().mockResolvedValue(options.subscription ?? null),
      subscribe: vi.fn(options.subscribe ?? (async () => ({
        endpoint: 'https://push.example/one',
        toJSON: () => ({ keys: { p256dh: 'key', auth: 'secret' } }),
      }))),
    }

    // `isSupported` only asks whether the two names are there, so a marker is
    // enough — and an empty class is not something to leave in a test file.
    vi.stubGlobal('window', { PushManager: 'present', Notification: 'present' })
    vi.stubGlobal('navigator', {
      serviceWorker: { ready: Promise.resolve({ pushManager }) },
    })
    vi.stubGlobal('Notification', {
      permission: options.permission ?? 'default',
      requestPermission: vi.fn(options.requestPermission ?? (async () => 'granted' as NotificationPermission)),
    })

    return pushManager
  }

  it('reports a browser that cannot do any of this', async () => {
    vi.stubGlobal('window', {})
    vi.stubGlobal('navigator', {})
    installFeatureGlobals(pushApi())

    const push = usePushNotifications()
    await push.resolve()

    expect(push.state.value).toBe('unsupported')
  })

  /** A deployment without VAPID keys is a supported state, not a broken one. */
  it('reports a deployment that sends nothing', async () => {
    installBrowser()
    installFeatureGlobals(pushApi(false))

    const push = usePushNotifications()
    await push.resolve()

    expect(push.state.value).toBe('unavailable')
  })

  it('treats a key that cannot be fetched as unavailable', async () => {
    installBrowser()
    const api = pushApi()
    api.notifications.key.mockRejectedValueOnce(new Error('offline'))
    const { report } = installFeatureGlobals(api)

    const push = usePushNotifications()
    await push.resolve()

    expect(push.state.value).toBe('unavailable')
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'notifications', action: 'key' })
  })

  /** The one state the app cannot undo, so saying so is the useful thing left. */
  it('reports a browser that has refused for good', async () => {
    installBrowser({ permission: 'denied' })
    installFeatureGlobals(pushApi())

    const push = usePushNotifications()
    await push.resolve()

    expect(push.state.value).toBe('blocked')
  })

  it('knows whether this device is already subscribed', async () => {
    installBrowser()
    installFeatureGlobals(pushApi())

    const off = usePushNotifications()
    await off.resolve()
    expect(off.state.value).toBe('off')

    installBrowser({ subscription: { endpoint: 'https://push.example/one' } })
    const on = usePushNotifications()
    await on.resolve()
    expect(on.state.value).toBe('on')
  })

  it('asks the browser and registers what it hands back', async () => {
    const pushManager = installBrowser()
    const api = pushApi()
    installFeatureGlobals(api)

    const push = usePushNotifications()
    await push.enable()

    expect(pushManager.subscribe).toHaveBeenCalledWith(
      expect.objectContaining({ userVisibleOnly: true }),
    )
    expect(api.notifications.subscribe).toHaveBeenCalledWith('https://push.example/one', 'key', 'secret')
    expect(push.state.value).toBe('on')
  })

  it('stays off when the person dismisses the prompt, and blocks when they refuse', async () => {
    installBrowser({ requestPermission: async () => 'default' as NotificationPermission })
    installFeatureGlobals(pushApi())

    const dismissed = usePushNotifications()
    await dismissed.enable()
    expect(dismissed.state.value).toBe('off')

    installBrowser({ permission: 'denied', requestPermission: async () => 'denied' as NotificationPermission })
    const refused = usePushNotifications()
    await refused.enable()
    expect(refused.state.value).toBe('blocked')
  })

  it('does not ask when there is no key to subscribe with', async () => {
    installBrowser()
    installFeatureGlobals(pushApi(false))

    const push = usePushNotifications()
    await push.enable()

    expect(push.state.value).toBe('unavailable')
    expect(Notification.requestPermission).not.toHaveBeenCalled()
  })

  /**
   * The browser's subscription goes first: telling the server first and then
   * failing to unsubscribe would leave a device receiving pushes the server no
   * longer knows it is sending.
   */
  it('unsubscribes the browser before the server', async () => {
    const order: string[] = []
    const unsubscribe = vi.fn(async () => {
      order.push('browser')
      return true
    })

    installBrowser({ subscription: { endpoint: 'https://push.example/one', unsubscribe } })
    const api = pushApi()
    api.notifications.unsubscribe.mockImplementation(async () => {
      order.push('server')
    })
    installFeatureGlobals(api)

    const push = usePushNotifications()
    await push.disable()

    expect(order).toEqual(['browser', 'server'])
    expect(push.state.value).toBe('off')
  })

  it('has nothing to disable when this device never subscribed', async () => {
    installBrowser()
    const api = pushApi()
    installFeatureGlobals(api)

    const push = usePushNotifications()
    await push.disable()

    expect(api.notifications.unsubscribe).not.toHaveBeenCalled()
    expect(push.state.value).toBe('off')
  })

  it('reports a refusal and works out where it stands again', async () => {
    installBrowser({
      subscribe: async () => {
        throw new Error('refused')
      },
    })
    const api = pushApi()
    const { report } = installFeatureGlobals(api)

    const push = usePushNotifications()
    await push.enable()

    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'notifications', action: 'enable' })
    expect(push.state.value).toBe('off')
  })

  it('does one thing at a time', async () => {
    installBrowser()
    const api = pushApi()
    let release: (value: unknown) => void = () => {}
    api.notifications.key.mockImplementation(() => new Promise((resolve) => {
      release = resolve
    }))
    installFeatureGlobals(api)

    const push = usePushNotifications()
    const first = push.enable()
    await nextTick()

    expect(push.isBusy.value).toBe(true)
    await push.enable()

    release({ isAvailable: false, publicKey: null })
    await first
  })
})

describe('the reads that were never exercised', () => {
  function activity(overrides: Record<string, unknown> = {}) {
    return {
      id: 'activity-1',
      person: person(),
      kind: 'TaskCompleted',
      subject: 'Laufen',
      amount: 1,
      occurredAt: '2026-09-09T09:00:00Z',
      kudosCount: 0,
      hasMyKudos: false,
      ...overrides,
    }
  }

  /**
   * The overview behind the bell is its own read rather than a slice of the
   * start screen's, so that opening it does not refetch four endpoints — which
   * means it also has its own failure and its own kudos path.
   */
  it('loads the activity overview and cheers from it', async () => {
    const api = {
      activity: {
        feed: vi.fn().mockResolvedValue([activity()]),
        toggleKudos: vi.fn().mockResolvedValue(activity({ kudosCount: 1, hasMyKudos: true })),
      },
    }

    const { show } = installFeatureGlobals(api)
    const overview = useActivityOverview()

    await vi.waitFor(() => expect(overview.feed.value).toHaveLength(1))
    expect(overview.isLoading.value).toBe(false)

    await overview.toggleKudos('activity-1')

    expect(overview.feed.value[0]).toMatchObject({ kudosCount: 1, hasMyKudos: true })
    expect(show).toHaveBeenCalledWith(de.toast.kudosSent)
  })

  it('says nothing when the cheer is taken back rather than given', async () => {
    const api = {
      activity: {
        feed: vi.fn().mockResolvedValue([activity({ kudosCount: 1, hasMyKudos: true })]),
        toggleKudos: vi.fn().mockResolvedValue(activity({ kudosCount: 0, hasMyKudos: false })),
      },
    }

    const { show } = installFeatureGlobals(api)
    const overview = useActivityOverview()

    await vi.waitFor(() => expect(overview.feed.value).toHaveLength(1))
    await overview.toggleKudos('activity-1')

    expect(show).not.toHaveBeenCalled()
  })

  it('reports both of the overview\'s failures', async () => {
    const api = {
      activity: {
        feed: vi.fn().mockRejectedValueOnce(new Error('offline')).mockResolvedValue([]),
        toggleKudos: vi.fn().mockRejectedValue(new Error('gone')),
      },
    }

    const { report } = installFeatureGlobals(api)
    const overview = useActivityOverview()

    await vi.waitFor(() => expect(overview.error.value).toEqual(failure))
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'feed', action: 'load' })

    await overview.toggleKudos('activity-1')
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'feed', action: 'kudos' })
  })

  /**
   * A vote is final, so the card is removed rather than the list refreshed —
   * and a round trip between "confirm" and the next photograph is a round trip
   * spent looking at a card somebody has finished with.
   */
  it('drops the card it has just voted on', async () => {
    const api = {
      proofs: {
        pending: vi.fn().mockResolvedValue([
          { proof: { id: 'proof-1' }, goalTitle: 'Laufen', goalIcon: 'medal' },
          { proof: { id: 'proof-2' }, goalTitle: 'Lesen', goalIcon: 'book-open' },
        ]),
        vote: vi.fn().mockResolvedValue({ id: 'proof-1' }),
      },
    }

    const { show } = installFeatureGlobals(api)
    const pending = usePendingProofs()

    await vi.waitFor(() => expect(pending.proofs.value).toHaveLength(2))

    await pending.vote('proof-1', 'Confirm')

    expect(pending.proofs.value).toHaveLength(1)
    expect(show).toHaveBeenCalledWith(de.toast.voteCast)
  })

  /**
   * Something is out of step — the vote closed, or somebody got there first.
   * Reloading is the honest answer rather than leaving a card that cannot be
   * voted on.
   */
  it('reloads when a vote is refused, and votes one at a time', async () => {
    const api = {
      proofs: {
        pending: vi.fn().mockResolvedValue([{ proof: { id: 'proof-1' }, goalTitle: 'Laufen', goalIcon: 'medal' }]),
        vote: vi.fn().mockRejectedValueOnce(new Error('closed')),
      },
    }

    const { report } = installFeatureGlobals(api)
    const pending = usePendingProofs()

    await vi.waitFor(() => expect(pending.proofs.value).toHaveLength(1))

    await pending.vote('proof-1', 'Doubt')

    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'proofs', action: 'vote' })
    expect(api.proofs.pending).toHaveBeenCalledTimes(2)

    let release: (value: unknown) => void = () => {}
    api.proofs.vote.mockImplementation(() => new Promise((resolve) => {
      release = resolve
    }))

    const first = pending.vote('proof-1', 'Confirm')
    await nextTick()
    expect(pending.isVoting.value).toBe(true)

    await pending.vote('proof-1', 'Confirm')
    release({ id: 'proof-1' })
    await first
  })

  /**
   * A goal nobody shares is believed at once; a shared one is now waiting.
   * Saying which is the difference between "done" and "handed in".
   */
  it('says whether a delivered photograph was believed or is waiting', async () => {
    const api = {
      proofs: {
        pending: vi.fn().mockResolvedValue([]),
        submit: vi.fn().mockResolvedValue({ status: 'Confirmed' }),
      },
    }

    const { show } = installFeatureGlobals(api)
    const delivery = useProofDelivery()

    expect(await delivery.deliver('goal-1', { id: 'image-1' } as never)).toBe(true)
    expect(show).toHaveBeenCalledWith(de.toast.proofConfirmed)

    api.proofs.submit.mockResolvedValueOnce({ status: 'Voting' })
    await delivery.deliver('goal-1', { id: 'image-2' } as never)
    expect(show).toHaveBeenCalledWith(de.toast.proofDelivered)
  })

  it('answers false rather than throwing when a window refuses the photograph', async () => {
    const api = {
      proofs: {
        pending: vi.fn().mockResolvedValue([]),
        submit: vi.fn().mockRejectedValueOnce(new Error('closed')),
      },
    }

    const { report } = installFeatureGlobals(api)
    const delivery = useProofDelivery()

    expect(await delivery.deliver('goal-1', { id: 'image-1' } as never)).toBe(false)
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'proofs', action: 'deliver' })
  })

  it('delivers one photograph at a time', async () => {
    let release: (value: unknown) => void = () => {}
    const api = {
      proofs: {
        pending: vi.fn().mockResolvedValue([]),
        submit: vi.fn(() => new Promise((resolve) => {
          release = resolve
        })),
      },
    }

    installFeatureGlobals(api)
    const delivery = useProofDelivery()

    const first = delivery.deliver('goal-1', { id: 'image-1' } as never)
    await nextTick()

    expect(delivery.isDelivering.value).toBe(true)
    expect(await delivery.deliver('goal-1', { id: 'image-2' } as never)).toBe(false)

    release({ status: 'Confirmed' })
    await first
  })
})

describe('the profile paths that were left over', () => {
  function profileApi() {
    return {
      profile: {
        get: vi.fn().mockResolvedValue({ person: person({ displayName: 'Mara' }) }),
        person: vi.fn().mockResolvedValue({ person: person(), friendship: 'Friends' }),
        update: vi.fn().mockResolvedValue({ person: person({ displayName: 'Mara K.' }) }),
      },
      images: { remove: vi.fn().mockResolvedValue(undefined) },
    }
  }

  it('renames, chooses a picture, and saves one thing at a time', async () => {
    const api = profileApi()
    const { show } = installFeatureGlobals(api)
    const profile = useProfile()

    await vi.waitFor(() => expect(profile.profile.value).not.toBeNull())

    await profile.rename('Mara K.')
    expect(api.profile.update).toHaveBeenCalledWith({ displayName: 'Mara K.' })
    expect(show).toHaveBeenCalledWith(de.toast.profileSaved)

    await profile.chooseAvatar('image-1')
    expect(api.profile.update).toHaveBeenCalledWith({ avatarImageId: 'image-1' })
  })

  /**
   * The picture itself goes, not just the profile's reference to it: an image
   * nothing points at would still be sitting in somebody's storage allowance.
   */
  it('deletes the picture rather than merely unpointing the profile at it', async () => {
    const api = profileApi()
    const { show, report } = installFeatureGlobals(api)
    const profile = useProfile()

    await vi.waitFor(() => expect(profile.profile.value).not.toBeNull())

    await profile.removeAvatar('image-1')

    expect(api.images.remove).toHaveBeenCalledWith('image-1')
    expect(show).toHaveBeenCalledWith(de.toast.photoRemoved)

    api.images.remove.mockRejectedValueOnce(new Error('gone'))
    await profile.removeAvatar('image-1')
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'images', action: 'delete' })
  })

  /**
   * A stale link is an ordinary outcome and gets its own calm state, rather
   * than the generic "something went wrong".
   */
  it('tells a missing person apart from a broken connection', async () => {
    const api = profileApi()
    api.profile.person.mockRejectedValueOnce(new Error('gone'))

    const report = vi.fn(() => ({ ...failure, kind: 'notFound' as const }))
    vi.stubGlobal('useQ2Api', () => api)
    vi.stubGlobal('useErrorReporter', () => ({ report }))
    vi.stubGlobal('useToastMessage', () => ({ show: vi.fn() }))
    vi.stubGlobal('useMessages', () => ref(de))

    const missing = usePersonProfile(computed(() => 'person-1'))

    await vi.waitFor(() => expect(missing.isMissing.value).toBe(true))
    expect(missing.error.value).toBeNull()
  })
})
