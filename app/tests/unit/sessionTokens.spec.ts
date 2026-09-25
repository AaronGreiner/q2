import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '~/api/errors'
import type { SessionTokens } from '~/api/types'
import { createTokenKeeper, type RefreshTokenStorage } from '~/utils/sessionTokens'

function memoryStorage(initial: string | null = null) {
  let value = initial

  const storage: RefreshTokenStorage = {
    read: vi.fn(async () => value),
    write: vi.fn(async (next: string) => {
      value = next
    }),
    clear: vi.fn(async () => {
      value = null
    }),
  }

  return { storage, current: () => value }
}

function pair(n: number, expiresIn = 60): SessionTokens {
  return { tokenType: 'Bearer', accessToken: `access-${n}`, refreshToken: `refresh-${n}`, expiresIn }
}

describe('the token keeper', () => {
  it('is signed out when nothing was ever stored', async () => {
    const { storage } = memoryStorage()
    const refresh = vi.fn()
    const keeper = createTokenKeeper({ storage, refresh })

    await expect(keeper.accessToken()).resolves.toBeNull()
    expect(refresh).not.toHaveBeenCalled()
  })

  it('keeps the refresh token and hands out the access token until just before it expires', async () => {
    let now = 1_000_000
    const { storage, current } = memoryStorage()
    const refresh = vi.fn().mockResolvedValue(pair(2))
    const keeper = createTokenKeeper({ storage, refresh, now: () => now })

    await keeper.adopt(pair(1))
    expect(current()).toBe('refresh-1')
    await expect(keeper.accessToken()).resolves.toBe('access-1')

    // Five seconds of margin: a token about to run out is not handed out.
    now += 55_000
    await expect(keeper.accessToken()).resolves.toBe('access-2')
    expect(refresh).toHaveBeenCalledWith('refresh-1')
    expect(current()).toBe('refresh-2')
  })

  it('refreshes once for however many calls are waiting', async () => {
    const { storage } = memoryStorage('refresh-1')
    let answer: (tokens: SessionTokens) => void = () => undefined
    const refresh = vi.fn(() => new Promise<SessionTokens>((resolve) => {
      answer = resolve
    }))
    const keeper = createTokenKeeper({ storage, refresh })

    const waiting = [keeper.accessToken(), keeper.accessToken(), keeper.accessToken()]
    await vi.waitFor(() => expect(refresh).toHaveBeenCalledOnce())
    answer(pair(2))

    await expect(Promise.all(waiting)).resolves.toEqual(['access-2', 'access-2', 'access-2'])
    expect(refresh).toHaveBeenCalledOnce()
  })

  it('signs out when the server refuses the refresh', async () => {
    const { storage, current } = memoryStorage('refresh-1')
    const refresh = vi.fn().mockRejectedValue(new ApiError({ kind: 'unauthorized', status: 401 }))
    const keeper = createTokenKeeper({ storage, refresh })

    await expect(keeper.accessToken()).resolves.toBeNull()
    expect(current()).toBeNull()
  })

  it('keeps the session when the refresh merely could not be sent', async () => {
    const { storage, current } = memoryStorage('refresh-1')
    const refresh = vi.fn()
      .mockRejectedValueOnce(new ApiError({ kind: 'network' }))
      .mockResolvedValueOnce(pair(2))
    const keeper = createTokenKeeper({ storage, refresh })

    await expect(keeper.accessToken()).rejects.toMatchObject({ kind: 'network' })
    expect(current()).toBe('refresh-1')

    await expect(keeper.accessToken()).resolves.toBe('access-2')
  })

  it('refreshes again after the access token is invalidated', async () => {
    const { storage } = memoryStorage()
    const refresh = vi.fn().mockResolvedValue(pair(2))
    const keeper = createTokenKeeper({ storage, refresh })

    await keeper.adopt(pair(1))
    keeper.invalidate()

    await expect(keeper.accessToken()).resolves.toBe('access-2')
    expect(refresh).toHaveBeenCalledWith('refresh-1')
  })

  it('does not let a refresh that was under way sign somebody back in', async () => {
    const { storage, current } = memoryStorage('refresh-1')
    let answer: (tokens: SessionTokens) => void = () => undefined
    const refresh = vi.fn(() => new Promise<SessionTokens>((resolve) => {
      answer = resolve
    }))
    const keeper = createTokenKeeper({ storage, refresh })

    const pending = keeper.accessToken()
    await vi.waitFor(() => expect(refresh).toHaveBeenCalledOnce())

    await keeper.clear()
    answer(pair(2))

    await expect(pending).resolves.toBeNull()
    expect(current()).toBeNull()
    await expect(keeper.accessToken()).resolves.toBeNull()
  })

  it('does not sign out a newer session when an older refresh is refused', async () => {
    const { storage, current } = memoryStorage('refresh-1')
    let refuse: (error: unknown) => void = () => undefined
    const refresh = vi.fn(() => new Promise<SessionTokens>((_, reject) => {
      refuse = reject
    }))
    const keeper = createTokenKeeper({ storage, refresh })

    const pending = keeper.accessToken()
    await vi.waitFor(() => expect(refresh).toHaveBeenCalledOnce())

    // Signed out and straight back in while the old refresh was on its way.
    await keeper.clear()
    await keeper.adopt(pair(3))
    refuse(new ApiError({ kind: 'unauthorized', status: 401 }))

    await expect(pending).resolves.toBeNull()
    expect(current()).toBe('refresh-3')
    await expect(keeper.accessToken()).resolves.toBe('access-3')
  })
})
