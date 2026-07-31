import { computed, nextTick, reactive, ref, watch } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { de, en } from '~/i18n/messages'
import { ApiError } from '~/api/errors'
import { useAppSettings, useTheme } from '~/composables/useAppSettings'
import { useLanguage, useMessages } from '~/composables/useMessages'
import { useNow, useTimeZoneOffset } from '~/composables/useNow'
import { useQ2Api } from '~/composables/useQ2Api'
import { signInMessage, useSession } from '~/composables/useSession'
import { useToastMessage } from '~/composables/useToastMessage'

const failure = {
  kind: 'network' as const,
  isExpected: true,
  status: null,
  fieldErrors: {},
  traceId: null,
  errorId: null,
  reason: null,
}

function installVueGlobals() {
  vi.stubGlobal('ref', ref)
  vi.stubGlobal('computed', computed)
  vi.stubGlobal('watch', watch)
}

function installState() {
  const state = new Map<string, ReturnType<typeof ref>>()

  vi.stubGlobal('useState', (key: string, initial: () => unknown) => {
    if (!state.has(key)) state.set(key, ref(initial()))
    return state.get(key)
  })

  return state
}

function installAsyncData() {
  vi.stubGlobal('useAsyncData', (
    _key: unknown,
    handler: () => Promise<unknown>,
    options: { default?: () => unknown } = {},
  ) => {
    const data = ref(options.default?.())
    const refresh = vi.fn(async () => {
      data.value = await handler()
      return data.value
    })
    void refresh()
    return { data, status: ref('pending'), refresh }
  })
}

beforeEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  installVueGlobals()
  installState()
  vi.stubGlobal('useNuxtApp', () => ({
    runWithContext: <T>(callback: () => T) => callback(),
  }))
})

describe('language and time state', () => {
  it('defaults to German and switches the complete catalogue reactively', async () => {
    const language = useLanguage()
    const messages = useMessages()

    expect(language.value).toBe('German')
    expect(messages.value).toBe(de)

    language.value = 'English'
    await nextTick()
    expect(messages.value).toBe(en)
  })

  it('shares one serialisable now value', () => {
    vi.setSystemTime(new Date('2026-07-31T10:00:00Z'))

    const first = useNow()
    const second = useNow()

    expect(first).toBe(second)
    expect(first.value).toBe(Date.parse('2026-07-31T10:00:00Z'))
    vi.useRealTimers()
  })

  it('applies the browser time-zone offset only after mounting', () => {
    const mounted = vi.fn((callback: () => void) => callback())
    vi.stubGlobal('onMounted', mounted)
    const offset = useTimeZoneOffset()

    expect(mounted).toHaveBeenCalledOnce()
    expect(offset.value).toBe(-new Date().getTimezoneOffset())
  })
})

describe('theme and settings', () => {
  it('translates all theme values and toggles what is currently visible', () => {
    const colorMode = reactive({ preference: 'system', value: 'dark' })
    vi.stubGlobal('useColorMode', () => colorMode)
    const theme = useTheme()

    expect(theme.preference.value).toBe('System')
    expect(theme.isDark.value).toBe(true)

    theme.toggle()
    expect(colorMode.preference).toBe('light')

    theme.preference.value = 'Dark'
    expect(colorMode.preference).toBe('dark')
    theme.preference.value = 'System'
    expect(colorMode.preference).toBe('system')

    colorMode.value = 'light'
    theme.toggle()
    expect(colorMode.preference).toBe('dark')
  })

  it('loads, applies and saves preferences optimistically', async () => {
    installAsyncData()
    const colorMode = reactive({ preference: 'system', value: 'light' })
    const language = ref<'German' | 'English'>('German')
    const settings = { theme: 'Dark', language: 'English', reminders: true, weeklyReview: false }
    const api = {
      settings: {
        get: vi.fn().mockResolvedValue(settings),
        update: vi.fn().mockImplementation(async change => ({ ...settings, ...change })),
      },
    }
    const report = vi.fn(() => failure)

    vi.stubGlobal('useColorMode', () => colorMode)
    vi.stubGlobal('useLanguage', () => language)
    vi.stubGlobal('useQ2Api', () => api)
    vi.stubGlobal('useErrorReporter', () => ({ report }))

    const state = useAppSettings()
    await vi.waitFor(() => expect(state.settings.value).not.toBeNull())
    expect(colorMode.preference).toBe('dark')
    expect(language.value).toBe('English')

    await state.update({ theme: 'Light', language: 'German' })
    expect(colorMode.preference).toBe('light')
    expect(language.value).toBe('German')
    expect(state.settings.value).toMatchObject({ theme: 'Light' })
    expect(state.isSaving.value).toBe(false)
  })

  it('keeps defaults usable when loading or saving preferences fails', async () => {
    installAsyncData()
    const colorMode = reactive({ preference: 'system', value: 'light' })
    const language = ref<'German' | 'English'>('German')
    const api = {
      settings: {
        get: vi.fn().mockRejectedValue(new Error('load')),
        update: vi.fn().mockRejectedValue(new Error('save')),
      },
    }
    const report = vi.fn(() => failure)

    vi.stubGlobal('useColorMode', () => colorMode)
    vi.stubGlobal('useLanguage', () => language)
    vi.stubGlobal('useQ2Api', () => api)
    vi.stubGlobal('useErrorReporter', () => ({ report }))

    const state = useAppSettings()
    await vi.waitFor(() => expect(api.settings.get).toHaveBeenCalledOnce())
    expect(state.settings.value).toBeNull()

    await state.update({ language: 'English' })
    expect(language.value).toBe('English')
    expect(state.isSaving.value).toBe(false)
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'settings', action: 'load' })
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'settings', action: 'update' })
  })
})

describe('session state', () => {
  function sessionApi() {
    const session = { person: { id: 'person-1', displayName: 'Mara' }, email: 'mara@example.test' }
    return {
      session,
      api: {
        accounts: {
          session: vi.fn().mockResolvedValue(session),
          login: vi.fn().mockResolvedValue(session),
          register: vi.fn().mockResolvedValue(session),
          logout: vi.fn().mockResolvedValue(undefined),
        },
      },
    }
  }

  it('resolves once and adopts login and registration answers', async () => {
    const { api, session } = sessionApi()
    vi.stubGlobal('useQ2Api', () => api)
    const clearNuxtData = vi.fn()
    vi.stubGlobal('clearNuxtData', clearNuxtData)
    const state = useSession()

    await expect(state.resolve()).resolves.toEqual(session)
    await expect(state.resolve()).resolves.toEqual(session)
    expect(api.accounts.session).toHaveBeenCalledOnce()
    expect(state.person.value).toMatchObject({ displayName: 'Mara' })
    expect(state.isSignedIn.value).toBe(true)

    state.adopt(null)
    expect(state.isSignedIn.value).toBe(false)
    await expect(state.login({ email: 'mara@example.test', password: 'long-password' })).resolves.toEqual(session)
    await expect(state.register({ displayName: 'Mara', email: 'mara@example.test', password: 'long-password' })).resolves.toEqual(session)
    expect(clearNuxtData).toHaveBeenCalledTimes(2)
  })

  it('treats an unauthorized resolution as signed out and clears cached personal data', async () => {
    const { api } = sessionApi()
    api.accounts.session.mockRejectedValueOnce(new ApiError({ kind: 'unauthorized', status: 401 }))
    api.accounts.logout.mockRejectedValueOnce(new Error('offline'))
    const clearNuxtData = vi.fn()
    vi.stubGlobal('useQ2Api', () => api)
    vi.stubGlobal('clearNuxtData', clearNuxtData)
    const state = useSession()

    await expect(state.resolve()).resolves.toBeNull()
    expect(state.isResolved.value).toBe(true)
    expect(clearNuxtData).toHaveBeenCalledOnce()
    await expect(state.logout()).rejects.toThrow('offline')
    expect(state.session.value).toBeNull()
    expect(clearNuxtData).toHaveBeenCalledTimes(2)
  })

  it('does not cache a transient session failure as signed out', async () => {
    const { api, session } = sessionApi()
    api.accounts.session.mockRejectedValueOnce(new ApiError({ kind: 'network' }))
    const clearNuxtData = vi.fn()
    vi.stubGlobal('useQ2Api', () => api)
    vi.stubGlobal('clearNuxtData', clearNuxtData)
    const state = useSession()

    await expect(state.resolve()).rejects.toMatchObject({ kind: 'network' })
    expect(state.isResolved.value).toBe(false)
    expect(state.isSignedIn.value).toBe(false)
    expect(clearNuxtData).not.toHaveBeenCalled()

    await expect(state.resolve()).resolves.toEqual(session)
    expect(api.accounts.session).toHaveBeenCalledTimes(2)
    expect(state.isResolved.value).toBe(true)
  })
})

describe('sign-in wording', () => {
  it('maps every failure without exposing server prose', () => {
    expect(signInMessage(null, de)).toBeNull()
    expect(signInMessage({ ...failure, kind: 'validation' }, de)).toBeNull()
    expect(signInMessage({ ...failure, kind: 'unauthorized', reason: 'invalidCredentials' }, de)).toBe(de.auth.invalidCredentials)
    expect(signInMessage({ ...failure, kind: 'unauthorized', reason: 'lockedOut' }, de)).toBe(de.auth.lockedOut)
    expect(signInMessage({ ...failure, kind: 'network' }, de)).toBe(de.errors.network)
  })
})

describe('configured API and toast adapters', () => {
  it('configures one credentialed, non-retrying fetcher for every API module', async () => {
    const apiFetch = vi.fn().mockResolvedValue({})
    const create = vi.fn(() => apiFetch)
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { apiBaseUrl: 'https://api.example.test' } }))
    vi.stubGlobal('$fetch', { create })

    const api = useQ2Api()
    await api.accounts.session()
    await api.goals.list()
    await api.tasks.list()
    await api.activity.feed()
    await api.friends.get()
    await api.chats.list()
    await api.profile.get()
    await api.settings.get()

    expect(create).toHaveBeenCalledWith(expect.objectContaining({
      baseURL: 'https://api.example.test',
      credentials: 'include',
      retry: 0,
      timeout: 10_000,
      headers: { Accept: 'application/json' },
    }))
    expect(apiFetch).toHaveBeenCalledTimes(8)
  })

  it('uses the shared toast shape and masks private names', () => {
    const add = vi.fn()
    vi.stubGlobal('useToast', () => ({ add }))
    const toast = useToastMessage()

    toast.show({ emoji: '✅', text: 'Saved' })
    toast.show({ emoji: '👋', text: 'Hello Mara' }, { private: true })

    expect(add).toHaveBeenNthCalledWith(1, {
      title: '✅ Saved',
      color: 'primary',
      duration: 2200,
      ui: undefined,
    })
    expect(add).toHaveBeenNthCalledWith(2, expect.objectContaining({
      title: '👋 Hello Mara',
      ui: { title: 'sentry-mask' },
    }))
  })
})
