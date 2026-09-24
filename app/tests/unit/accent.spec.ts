import { computed, ref } from 'vue'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { accentNames, normalizeAccent, useAccent } from '~/composables/useAccent'

afterEach(() => vi.unstubAllGlobals())

describe('device accent preference', () => {
  it('accepts only the reviewed palette names, including malformed cookies', () => {
    for (const name of accentNames) expect(normalizeAccent(name)).toBe(name)
    for (const value of [undefined, null, '', 'red', '#abcdef', {}, ['sage']]) {
      expect(normalizeAccent(value)).toBe('iris')
    }
  })

  it('reads the SSR cookie and persists changes to the same device preference', () => {
    const cookie = ref('sage')
    const useCookie = vi.fn(() => cookie)
    vi.stubGlobal('useCookie', useCookie)
    vi.stubGlobal('computed', computed)
    const accent = useAccent()
    expect(accent.value).toBe('sage')
    accent.value = 'rose'
    expect(cookie.value).toBe('rose')
    cookie.value = 'invalid'
    expect(accent.value).toBe('iris')
    expect(useCookie).toHaveBeenCalledWith('q2-accent', expect.objectContaining({ sameSite: 'lax', path: '/' }))
  })
})
