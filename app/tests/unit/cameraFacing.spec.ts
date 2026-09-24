import { afterEach, describe, expect, it, vi } from 'vitest'
import { rememberFacing, rememberedFacing } from '../../app/utils/cameraFacing'

afterEach(() => vi.unstubAllGlobals())

function storage(initial: Record<string, string> = {}) {
  const values = new Map(Object.entries(initial))
  const store = {
    getItem: vi.fn((key: string) => values.get(key) ?? null),
    setItem: vi.fn((key: string, value: string) => void values.set(key, value)),
  }
  vi.stubGlobal('localStorage', store)
  return store
}

describe('the camera the photo screen opens with', () => {
  it('is the rear one until somebody has switched', () => {
    storage()
    expect(rememberedFacing()).toBe('environment')
  })

  it('is the one switched to last', () => {
    storage()
    rememberFacing('user')
    expect(rememberedFacing()).toBe('user')

    rememberFacing('environment')
    expect(rememberedFacing()).toBe('environment')
  })

  it('ignores a stored value it does not know', () => {
    storage({ 'q2-camera-facing': 'left' })
    expect(rememberedFacing()).toBe('environment')
  })

  it('falls back to the rear camera when storage is refused, without throwing', () => {
    const refused = () => {
      throw new Error('SecurityError')
    }
    vi.stubGlobal('localStorage', { getItem: refused, setItem: refused })

    expect(() => rememberFacing('user')).not.toThrow()
    expect(rememberedFacing()).toBe('environment')
  })

  it('does nothing during server rendering', () => {
    vi.stubGlobal('localStorage', undefined)

    expect(() => rememberFacing('user')).not.toThrow()
    expect(rememberedFacing()).toBe('environment')
  })
})
