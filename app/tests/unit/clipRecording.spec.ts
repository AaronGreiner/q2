import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { clipFormat, maxClipSeconds, openMicrophone, recordClip, stopStream } from '../../app/utils/clipRecording'

/**
 * Recording a proof clip (#124). Node has no `MediaRecorder`, which is the
 * point: what is tested is what q2 asks of one — which format, when it stops,
 * what comes back — with a stand-in that does exactly what a browser's does.
 */

afterEach(() => {
  vi.unstubAllGlobals()
  vi.useRealTimers()
})

describe('the format a clip is recorded in', () => {
  it('is H.264 with AAC wherever the browser can write it', () => {
    expect(clipFormat(() => true)).toEqual({ mimeType: 'video/mp4;codecs=avc1.42E01E,mp4a.40.2', withSound: true })
  })

  /** Chromium on Linux has no AAC encoder: a silent clip beats no clip. */
  it('is H.264 without sound where there is no AAC encoder', () => {
    const format = clipFormat(type => !type.includes('mp4a'))

    expect(format).toEqual({ mimeType: 'video/mp4;codecs=avc1.42E01E', withSound: false })
  })

  it('is plain MP4 where that is all the browser names, which is how Safari is asked', () => {
    expect(clipFormat(type => type === 'video/mp4')).toEqual({ mimeType: 'video/mp4', withSound: true })
  })

  /** A WebM some iPhones cannot play is worse than no video mode at all. */
  it('is none at all when only WebM can be written', () => {
    expect(clipFormat(type => type.startsWith('video/webm'))).toBeNull()
  })

  it('is none without a recorder, or when asking throws', () => {
    expect(clipFormat(undefined)).toBeNull()
    expect(clipFormat(() => {
      throw new Error('not here')
    })).toBeNull()
  })

  it('is asked of the browser\'s own recorder when none is given', () => {
    vi.stubGlobal('MediaRecorder', { isTypeSupported: (type: string) => type === 'video/mp4' })

    expect(clipFormat()?.mimeType).toBe('video/mp4')
  })

  it('stops at ten seconds, which is what the server allows', () => {
    expect(maxClipSeconds).toBe(10)
  })
})

describe('asking for the microphone', () => {
  function track() {
    return { stop: vi.fn() }
  }

  function stream(...tracks: ReturnType<typeof track>[]) {
    return { getTracks: () => tracks } as unknown as MediaStream
  }

  it('hands over the microphone when it is granted', async () => {
    const granted = stream(track())

    await expect(openMicrophone({ getUserMedia: vi.fn().mockResolvedValue(granted) })).resolves.toBe(granted)
  })

  it('records without sound when the microphone is refused', async () => {
    await expect(openMicrophone({ getUserMedia: vi.fn().mockRejectedValue(new Error('denied')) })).resolves.toBeNull()
    await expect(openMicrophone(undefined)).resolves.toBeNull()
  })

  /**
   * A permission request that never answers must not hold up the camera, and
   * a microphone that turns up after giving up is closed at once.
   */
  it('gives up on a request that never answers, and closes a late one', async () => {
    vi.useFakeTimers()
    let grant: (value: MediaStream) => void = () => {}
    const late = track()

    const asked = openMicrophone({
      getUserMedia: () => new Promise((resolve) => {
        grant = resolve
      }),
    }, 4000)
    await vi.advanceTimersByTimeAsync(4000)

    await expect(asked).resolves.toBeNull()

    grant(stream(late))
    await vi.runAllTimersAsync()
    expect(late.stop).toHaveBeenCalled()
  })
})

describe('recording a clip', () => {
  /** What a browser's recorder does, as far as q2 uses it. */
  class FakeRecorder {
    static last: FakeRecorder | null = null
    state: 'inactive' | 'recording' = 'inactive'
    ondataavailable: ((event: { data: Blob }) => void) | null = null
    onstop: (() => void) | null = null
    onerror: (() => void) | null = null
    timeslice: number | undefined

    constructor(public stream: unknown, public options: MediaRecorderOptions) {
      FakeRecorder.last = this
    }

    start(timeslice?: number) {
      this.timeslice = timeslice
      this.state = 'recording'
    }

    stop() {
      this.state = 'inactive'
      this.ondataavailable?.({ data: new Blob(['frames'], { type: 'video/mp4;codecs=avc1' }) })
      this.onstop?.()
    }
  }

  const format = { mimeType: 'video/mp4;codecs=avc1.42E01E,mp4a.40.2', withSound: true }

  beforeEach(() => {
    vi.useFakeTimers()
    vi.stubGlobal('MediaRecorder', FakeRecorder)
  })

  it('records in the chosen format at a modest bitrate, handing data over every second', () => {
    recordClip({} as MediaStream, format, { limitMs: 10_000 })

    expect(FakeRecorder.last?.options).toMatchObject({ mimeType: format.mimeType, videoBitsPerSecond: 2_500_000, audioBitsPerSecond: 128_000 })
    expect(FakeRecorder.last?.timeslice).toBe(1000)
  })

  it('asks for no sound bitrate when the format carries none', () => {
    recordClip({} as MediaStream, { mimeType: 'video/mp4;codecs=avc1.42E01E', withSound: false }, { limitMs: 10_000 })

    expect(FakeRecorder.last?.options).not.toHaveProperty('audioBitsPerSecond')
  })

  it('hands back an MP4 when it is stopped', async () => {
    const clip = recordClip({} as MediaStream, format, { limitMs: 10_000 })

    clip.stop()
    const recorded = await clip.done

    // Labelled without codecs: the media type the upload goes as.
    expect(recorded?.type).toBe('video/mp4')
    expect(recorded?.size).toBeGreaterThan(0)
  })

  it('stops itself at the limit and says how long it ran on the way', async () => {
    let now = 0
    const ticks: number[] = []
    const clip = recordClip({} as MediaStream, format, { limitMs: 10_000, now: () => now, onTick: elapsed => ticks.push(elapsed) })

    now = 4_000
    await vi.advanceTimersByTimeAsync(100)
    now = 10_250
    await vi.advanceTimersByTimeAsync(100)

    await expect(clip.done).resolves.toBeInstanceOf(Blob)
    expect(FakeRecorder.last?.state).toBe('inactive')
    expect(ticks).toEqual([4_000, 10_000])
  })

  /** Closing the screen while recording leaves nothing behind. */
  it('keeps nothing when it is thrown away', async () => {
    const clip = recordClip({} as MediaStream, format, { limitMs: 10_000 })

    clip.discard()

    await expect(clip.done).resolves.toBeNull()
  })

  it('ends with nothing rather than hanging when the recorder will not start', async () => {
    class Refusing extends FakeRecorder {
      override start(): void {
        throw new Error('NotSupportedError')
      }
    }
    vi.stubGlobal('MediaRecorder', Refusing)

    await expect(recordClip({} as MediaStream, format, { limitMs: 10_000 }).done).resolves.toBeNull()
  })

  it('ends with nothing when the recorder fails', async () => {
    const clip = recordClip({} as MediaStream, format, { limitMs: 10_000 })

    FakeRecorder.last?.onerror?.()

    await expect(clip.done).resolves.toBeNull()
  })
})

describe('stopping a stream', () => {
  it('stops every track, and tolerates there being none', () => {
    const tracks = [{ stop: vi.fn() }, { stop: vi.fn() }]

    stopStream({ getTracks: () => tracks } as unknown as MediaStream)
    stopStream(null)

    expect(tracks.every(track => track.stop.mock.calls.length === 1)).toBe(true)
  })
})
