import { beforeEach, describe, expect, it, vi } from 'vitest'

const platform = vi.hoisted(() => ({ native: false }))
const keychain = vi.hoisted(() => ({
  get: vi.fn(),
  set: vi.fn(),
  remove: vi.fn(),
}))

vi.mock('@capacitor/core', () => ({
  Capacitor: { isNativePlatform: () => platform.native },
}))

vi.mock('@aparajita/capacitor-secure-storage', () => ({
  SecureStorage: keychain,
  KeychainAccess: { whenUnlockedThisDeviceOnly: 1 },
}))

async function loadPlugin() {
  vi.stubGlobal('defineNuxtPlugin', (setup: () => unknown) => setup)
  vi.stubGlobal('useRuntimeConfig', () => ({ public: { apiBaseUrl: 'https://api.example.test' } }))
  const refresh = vi.fn()
  const create = vi.fn(() => refresh)
  vi.stubGlobal('$fetch', { create })

  const { default: setup } = await import('~/plugins/native.client')
  return { setup: setup as unknown as () => { provide: Record<string, unknown> }, create, refresh }
}

beforeEach(() => {
  vi.resetModules()
  vi.unstubAllGlobals()
  keychain.get.mockReset()
  keychain.set.mockReset()
  keychain.remove.mockReset()
})

describe('the native plugin', () => {
  it('provides nothing in the browser, which signs in with a cookie', async () => {
    platform.native = false
    const { setup } = await loadPlugin()

    expect(setup().provide).toEqual({ sessionTokens: null, imageCache: null })
  })

  it('keeps the refresh token in the Keychain, on this device only and never in iCloud', async () => {
    platform.native = true
    const { setup, create } = await loadPlugin()
    const { sessionTokens, imageCache } = setup().provide as {
      sessionTokens: { adopt: (tokens: object) => Promise<void>, clear: () => Promise<void> }
      imageCache: unknown
    }

    expect(imageCache).not.toBeNull()
    expect(create).toHaveBeenCalledWith(expect.objectContaining({
      baseURL: 'https://api.example.test',
      credentials: 'omit',
    }))

    await sessionTokens.adopt({ tokenType: 'Bearer', accessToken: 'a', refreshToken: 'r', expiresIn: 60 })
    expect(keychain.set).toHaveBeenCalledWith('refresh-token', 'r', false, false, 1)

    await sessionTokens.clear()
    expect(keychain.remove).toHaveBeenCalledWith('refresh-token', false)
  })

  it('refreshes with the stored token through its own client', async () => {
    platform.native = true
    const { setup, refresh } = await loadPlugin()
    keychain.get.mockResolvedValue('stored')
    refresh.mockResolvedValue({ tokenType: 'Bearer', accessToken: 'fresh', refreshToken: 'next', expiresIn: 60 })

    const { sessionTokens } = setup().provide as { sessionTokens: { accessToken: () => Promise<string | null> } }

    await expect(sessionTokens.accessToken()).resolves.toBe('fresh')
    expect(refresh).toHaveBeenCalledWith('/api/auth/token/refresh', { method: 'POST', body: { refreshToken: 'stored' } })
    expect(keychain.get).toHaveBeenCalledWith('refresh-token', false, false)
  })

  it('reads a missing or odd Keychain entry as no session', async () => {
    platform.native = true
    const { setup } = await loadPlugin()
    keychain.get.mockResolvedValue({ not: 'a string' })

    const { sessionTokens } = setup().provide as { sessionTokens: { accessToken: () => Promise<string | null> } }

    await expect(sessionTokens.accessToken()).resolves.toBeNull()
  })
})
