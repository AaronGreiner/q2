import { describe, expect, it } from 'vitest'
import { fit, uploadSizes } from '~/utils/images'

/**
 * The arithmetic behind an upload.
 *
 * `downscaleForUpload` itself is not tested here: it is a canvas, a bitmap
 * decoder and an encoder, none of which happy-dom implements, so a test of it
 * would be a test of three stubs agreeing with each other. What is worth
 * pinning down is the part that decides the size — that is where an off-by-one
 * turns into a rejected upload or a picture stored at four times what any
 * screen shows.
 */
describe('fitting a picture inside a square', () => {
  it('scales the long edge down to the limit and keeps the proportions', () => {
    expect(fit(4000, 3000, 1440)).toEqual({ width: 1440, height: 1080 })
    expect(fit(3000, 4000, 1440)).toEqual({ width: 1080, height: 1440 })
  })

  it('leaves a square square', () => {
    expect(fit(2048, 2048, 512)).toEqual({ width: 512, height: 512 })
  })

  it('never enlarges — that is bytes without detail', () => {
    expect(fit(200, 120, 1440)).toEqual({ width: 200, height: 120 })
  })

  it('rounds to whole pixels, because a canvas has no half ones', () => {
    const { width, height } = fit(1001, 333, 100)

    expect(Number.isInteger(width)).toBe(true)
    expect(Number.isInteger(height)).toBe(true)
  })

  it('keeps an extreme panorama at least one pixel tall', () => {
    // Rounding a 4000×3 strip down to 512 would otherwise produce a height of
    // zero, and a zero-sized canvas encodes to nothing at all.
    expect(fit(4000, 3, 512).height).toBeGreaterThanOrEqual(1)
  })

  it('stays inside what the server will accept', () => {
    // StoredImage.MaxDimension is 2048. Both upload sizes have to be under it
    // or the app would routinely send pictures its own backend rejects.
    expect(uploadSizes.avatar).toBeLessThanOrEqual(2048)
    expect(uploadSizes.proof).toBeLessThanOrEqual(2048)
  })
})
