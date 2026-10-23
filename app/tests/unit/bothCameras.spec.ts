import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mayTryBothAtOnce, openFrontBeside, rememberNotBothAtOnce, streamIsLive } from '../../app/utils/bothCameras'

/**
 * Whether the front camera can run beside the rear one (#125). A phone that
 * cannot takes the two pictures in turn; what matters here is telling the two
 * apart honestly, and never leaving a camera open that is not being used.
 */

function track(deviceId: string, state: Partial<MediaStreamTrack> = {}) {
  return {
    readyState: 'live',
    muted: false,
    stop: vi.fn(),
    getSettings: () => ({ deviceId }),
    ...state,
  }
}

function stream(...tracks: ReturnType<typeof track>[]) {
  return { getVideoTracks: () => tracks, getTracks: () => tracks } as unknown as MediaStream
}

let stored: Map<string, string>

beforeEach(() => {
  vi.useFakeTimers()
  stored = new Map()
  vi.stubGlobal('localStorage', {
    getItem: (key: string) => stored.get(key) ?? null,
    setItem: (key: string, value: string) => void stored.set(key, value),
  })
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.useRealTimers()
})

async function settle<T>(promise: Promise<T>): Promise<T> {
  await vi.runAllTimersAsync()
  return promise
}

describe('the front camera beside the rear one', () => {
  it('runs where both stay live', async () => {
    const front = stream(track('front'))
    const devices = {
      getUserMedia: vi.fn().mockResolvedValue(front),
      enumerateDevices: vi.fn(),
    }

    await expect(settle(openFrontBeside(stream(track('rear')), devices))).resolves.toBe(front)
    expect(devices.getUserMedia).toHaveBeenCalledWith({ video: { facingMode: { exact: 'user' } }, audio: false })
    expect(mayTryBothAtOnce()).toBe(true)
  })

  /** A desktop's camera, or a test's fake one, says nothing about which way it faces. */
  it('asks for a different camera when none says it faces the user', async () => {
    const front = stream(track('second'))
    const getUserMedia = vi.fn()
      .mockRejectedValueOnce(new Error('OverconstrainedError'))
      .mockResolvedValueOnce(front)
    const devices = {
      getUserMedia,
      enumerateDevices: vi.fn().mockResolvedValue([
        { kind: 'videoinput', deviceId: 'rear' },
        { kind: 'audioinput', deviceId: 'mic' },
        { kind: 'videoinput', deviceId: 'second' },
      ]),
    }

    await expect(settle(openFrontBeside(stream(track('rear')), devices))).resolves.toBe(front)
    expect(getUserMedia).toHaveBeenLastCalledWith({ video: { deviceId: { exact: 'second' } }, audio: false })
  })

  /** What WebKit does: the second camera opens, and the first goes quiet. */
  it('does not count a phone that muted the rear camera to open the front one', async () => {
    const rear = track('rear')
    const front = stream(track('front'))
    const devices = {
      getUserMedia: vi.fn(async () => {
        Object.assign(rear, { muted: true })
        return front
      }),
      enumerateDevices: vi.fn(),
    }

    await expect(settle(openFrontBeside(stream(rear), devices))).resolves.toBeNull()

    // Closed again, and remembered, so the next opening does not try.
    expect(front.getTracks()[0]!.stop).toHaveBeenCalled()
    expect(mayTryBothAtOnce()).toBe(false)
  })

  it('does not count the same camera opened twice', async () => {
    const devices = {
      getUserMedia: vi.fn().mockResolvedValue(stream(track('rear'))),
      enumerateDevices: vi.fn(),
    }

    await expect(settle(openFrontBeside(stream(track('rear')), devices))).resolves.toBeNull()
  })

  it('remembers a phone that refused the second camera outright', async () => {
    const devices = {
      getUserMedia: vi.fn().mockRejectedValue(new Error('NotReadableError')),
      enumerateDevices: vi.fn().mockResolvedValue([{ kind: 'videoinput', deviceId: 'rear' }]),
    }

    await expect(settle(openFrontBeside(stream(track('rear')), devices))).resolves.toBeNull()
    expect(mayTryBothAtOnce()).toBe(false)
  })

  it('does not ask again on a device that has said no', async () => {
    rememberNotBothAtOnce()
    const devices = { getUserMedia: vi.fn(), enumerateDevices: vi.fn() }

    await expect(openFrontBeside(stream(track('rear')), devices)).resolves.toBeNull()
    expect(devices.getUserMedia).not.toHaveBeenCalled()
  })

  it('keeps trying when storage is refused, rather than failing', () => {
    const refused = () => {
      throw new Error('SecurityError')
    }
    vi.stubGlobal('localStorage', { getItem: refused, setItem: refused })

    expect(() => rememberNotBothAtOnce()).not.toThrow()
    expect(mayTryBothAtOnce()).toBe(true)
  })
})

describe('whether a stream can still be shown', () => {
  it('is live while its picture track is live and not muted', () => {
    expect(streamIsLive(stream(track('rear')))).toBe(true)
    expect(streamIsLive(stream(track('rear', { muted: true })))).toBe(false)
    expect(streamIsLive(stream(track('rear', { readyState: 'ended' })))).toBe(false)
    expect(streamIsLive(null)).toBe(false)
  })
})
