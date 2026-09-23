import { afterEach, describe, expect, it, vi } from 'vitest'
import { hapticTap } from '../../app/utils/haptics'

afterEach(() => vi.unstubAllGlobals())

function browser(reducedMotion = false) {
  const vibrate = vi.fn().mockReturnValue(true)
  const matchMedia = vi.fn().mockReturnValue({ matches: reducedMotion })
  vi.stubGlobal('navigator', { vibrate })
  vi.stubGlobal('window', { matchMedia })
  return { vibrate, matchMedia }
}

describe('haptic tap', () => {
  it('requests one brief pulse with the navigator receiver intact', () => {
    const { vibrate } = browser()
    hapticTap()
    expect(vibrate).toHaveBeenCalledExactlyOnceWith(15)
    expect(vibrate.mock.contexts[0]).toBe(navigator)
  })

  it('does nothing during server rendering', () => {
    const { vibrate } = browser()
    vi.stubGlobal('window', undefined)
    expect(hapticTap).not.toThrow()
    expect(vibrate).not.toHaveBeenCalled()
  })

  it('does nothing when the browser has no vibration API', () => {
    browser()
    vi.stubGlobal('navigator', {})
    expect(hapticTap).not.toThrow()
  })

  it('respects reduced motion and rechecks it on each interaction', () => {
    const { vibrate, matchMedia } = browser(true)
    hapticTap()
    expect(matchMedia).toHaveBeenCalledWith('(prefers-reduced-motion: reduce)')
    expect(vibrate).not.toHaveBeenCalled()
    matchMedia.mockReturnValue({ matches: false })
    hapticTap()
    expect(vibrate).toHaveBeenCalledOnce()
  })

  it('tolerates a device declining the pulse', () => {
    const { vibrate } = browser()
    vibrate.mockReturnValue(false)
    expect(hapticTap).not.toThrow()
  })

  it('does not let a blocked vibration interrupt the interaction', () => {
    const { vibrate } = browser()
    vibrate.mockImplementation(() => {
      throw new Error('Vibration blocked')
    })
    expect(hapticTap).not.toThrow()
  })
})
