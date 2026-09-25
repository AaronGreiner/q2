import { describe, expect, it, vi } from 'vitest'
import { createObjectUrlCache } from '~/utils/objectUrlCache'

function fakes() {
  let n = 0
  const create = vi.fn((_: Blob) => `blob:picture-${++n}`)
  const revoke = vi.fn()
  return { create, revoke }
}

const picture = () => Promise.resolve(new Blob(['bytes'], { type: 'image/jpeg' }))

describe('the picture cache', () => {
  it('loads a picture once and hands out the same address after that', async () => {
    const { create, revoke } = fakes()
    const cache = createObjectUrlCache({ create, revoke })
    const load = vi.fn(picture)

    await expect(cache.get('a', load)).resolves.toBe('blob:picture-1')
    await expect(cache.get('a', load)).resolves.toBe('blob:picture-1')
    expect(load).toHaveBeenCalledOnce()
  })

  it('evicts and revokes the least recently used picture beyond its capacity', async () => {
    const { create, revoke } = fakes()
    const cache = createObjectUrlCache({ capacity: 2, create, revoke })

    await cache.get('a', picture)
    await cache.get('b', picture)
    await cache.get('a', picture) // a is now the most recent
    await cache.get('c', picture)

    await vi.waitFor(() => expect(revoke).toHaveBeenCalledWith('blob:picture-2'))
    expect(revoke).toHaveBeenCalledOnce()

    const load = vi.fn(picture)
    await cache.get('a', load)
    expect(load).not.toHaveBeenCalled()
  })

  it('does not remember a picture that failed to load', async () => {
    const { create, revoke } = fakes()
    const cache = createObjectUrlCache({ create, revoke })

    await expect(cache.get('a', () => Promise.reject(new Error('offline')))).rejects.toThrow('offline')
    await expect(cache.get('a', picture)).resolves.toBe('blob:picture-1')
  })

  it('revokes everything on clear, so nothing survives a sign-out', async () => {
    const { create, revoke } = fakes()
    const cache = createObjectUrlCache({ create, revoke })

    await cache.get('a', picture)
    await cache.get('b', picture)
    cache.clear()

    await vi.waitFor(() => expect(revoke).toHaveBeenCalledTimes(2))

    const load = vi.fn(picture)
    await cache.get('a', load)
    expect(load).toHaveBeenCalledOnce()
  })
})
