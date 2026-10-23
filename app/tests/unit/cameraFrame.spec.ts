import { afterEach, describe, expect, it, vi } from 'vitest'
import { frameReady, grabFrame } from '../../app/utils/cameraFrame'
import { proofMediaOf } from '../../app/utils/proofMedia'

afterEach(() => vi.useRealTimers())

function canvas() {
  const context = { drawImage: vi.fn(), scale: vi.fn(), setTransform: vi.fn(), translate: vi.fn() }
  const element = {
    width: 0,
    height: 0,
    getContext: () => context,
    toBlob: (done: (blob: Blob | null) => void, type: string) => done(new Blob(['jpeg'], { type })),
  }
  return { context, element: element as unknown as HTMLCanvasElement }
}

describe('a frame from the live camera', () => {
  /**
   * The front camera is shown mirrored, like every phone shows it; the picture
   * kept is the scene the right way round (#125).
   */
  it('is drawn as the camera delivers it, never mirrored', async () => {
    const { context, element } = canvas()
    const video = { videoWidth: 1280, videoHeight: 720 } as HTMLVideoElement

    const frame = await grabFrame(video, () => element)

    expect(frame?.type).toBe('image/jpeg')
    expect(element.width).toBe(1280)
    expect(element.height).toBe(720)
    expect(context.drawImage).toHaveBeenCalledWith(video, 0, 0)
    expect(context.scale).not.toHaveBeenCalled()
    expect(context.setTransform).not.toHaveBeenCalled()
    expect(context.translate).not.toHaveBeenCalled()
  })

  it('is nothing while the camera has no picture yet', async () => {
    await expect(grabFrame({ videoWidth: 0, videoHeight: 0 } as HTMLVideoElement)).resolves.toBeNull()
    await expect(grabFrame(null)).resolves.toBeNull()
  })

  it('is waited for until the camera shows one, or for a few seconds at most', async () => {
    vi.useFakeTimers()
    const listeners: Array<() => void> = []
    const video = {
      videoWidth: 0,
      addEventListener: (_: string, listener: () => void) => listeners.push(listener),
      removeEventListener: vi.fn(),
    } as unknown as HTMLVideoElement

    let ready = false
    void frameReady(video, 3000).then(() => {
      ready = true
    })

    listeners[0]!()
    await vi.advanceTimersByTimeAsync(0)
    expect(ready).toBe(true)

    let gaveUp = false
    void frameReady(video, 3000).then(() => {
      gaveUp = true
    })
    await vi.advanceTimersByTimeAsync(3000)
    expect(gaveUp).toBe(true)

    await expect(frameReady({ videoWidth: 640 } as HTMLVideoElement)).resolves.toBeUndefined()
  })
})

describe('what a proof shows', () => {
  it('is one photograph for anything from before clips and both cameras', () => {
    expect(proofMediaOf({ imageId: 'image-1' })).toEqual({ imageId: 'image-1', media: 'Photo', posterImageId: null, insetImageId: null })
    expect(proofMediaOf({ imageId: 'clip-1', media: 'Video', posterImageId: 'still-1', insetImageId: 'front-1' }))
      .toEqual({ imageId: 'clip-1', media: 'Video', posterImageId: 'still-1', insetImageId: 'front-1' })
  })
})
