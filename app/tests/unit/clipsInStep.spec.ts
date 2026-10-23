import { describe, expect, it, vi } from 'vitest'
import { type StepClip, keepInStep, stepTolerance } from '../../app/utils/clipsInStep'

/**
 * Two clips filmed at once, played as one (#151). The corner follows the
 * large one: what has to hold is that it plays and stops with it, is put back
 * on the same moment when they drift or somebody seeks, and lets go once the
 * pair is broken up.
 */

class FakeClip extends EventTarget implements StepClip {
  currentTime = 0
  paused = true
  duration = 10
  playbackRate = 1

  play = vi.fn(async () => {
    this.paused = false
    this.dispatchEvent(new Event('play'))
  })

  pause = vi.fn(() => {
    this.paused = true
    this.dispatchEvent(new Event('pause'))
  })

  /** What the browser does a few times a second while playing. */
  tick(to: number) {
    this.currentTime = to
    this.dispatchEvent(new Event('timeupdate'))
  }
}

describe('keepInStep', () => {
  it('starts and stops the follower with the leader', async () => {
    const leader = new FakeClip()
    const follower = new FakeClip()
    keepInStep(leader, follower)

    await leader.play()
    expect(follower.play).toHaveBeenCalled()
    expect(follower.paused).toBe(false)

    leader.pause()
    expect(follower.pause).toHaveBeenCalled()
    expect(follower.paused).toBe(true)
  })

  it('puts the follower back on the leader\'s moment once they drift apart, and not before', () => {
    const leader = new FakeClip()
    const follower = new FakeClip()
    keepInStep(leader, follower)

    follower.currentTime = 3 + stepTolerance / 2
    leader.tick(3)
    expect(follower.currentTime).toBe(3 + stepTolerance / 2)

    follower.currentTime = 3.6
    leader.tick(3)
    expect(follower.currentTime).toBe(3)
  })

  it('follows a seek at once', () => {
    const leader = new FakeClip()
    const follower = new FakeClip()
    keepInStep(leader, follower)

    leader.currentTime = 7.5
    leader.dispatchEvent(new Event('seeked'))

    expect(follower.currentTime).toBe(7.5)
  })

  /**
   * A follower a frame shorter waits at its end for the leader to loop round,
   * rather than starting over on its own and running half a clip ahead.
   */
  it('lets a shorter follower wait at its end, and picks it up when the leader loops', async () => {
    const leader = new FakeClip()
    const follower = new FakeClip()
    follower.duration = 9.9
    keepInStep(leader, follower)
    await leader.play()

    follower.paused = true
    follower.play.mockClear()
    follower.currentTime = 9.9
    leader.tick(9.95)
    expect(follower.play).not.toHaveBeenCalled()

    leader.tick(0.1)
    expect(follower.currentTime).toBe(0.1)
    expect(follower.play).toHaveBeenCalled()
  })

  it('matches the leader\'s speed', () => {
    const leader = new FakeClip()
    const follower = new FakeClip()
    leader.playbackRate = 1.5
    keepInStep(leader, follower)
    expect(follower.playbackRate).toBe(1.5)

    leader.playbackRate = 2
    leader.dispatchEvent(new Event('ratechange'))
    expect(follower.playbackRate).toBe(2)
  })

  /** A swap makes the other one lead: the old pairing must stop pulling. */
  it('lets go when the pairing ends', async () => {
    const leader = new FakeClip()
    const follower = new FakeClip()
    const release = keepInStep(leader, follower)

    release()
    follower.currentTime = 5
    leader.tick(1)
    await leader.play()

    expect(follower.currentTime).toBe(5)
    expect(follower.play).not.toHaveBeenCalled()
  })

  /** A refused autoplay is the browser's answer, not an error: the corner keeps its frame. */
  it('swallows a refused play', async () => {
    const leader = new FakeClip()
    const follower = new FakeClip()
    follower.play.mockRejectedValueOnce(new Error('NotAllowedError'))
    keepInStep(leader, follower)

    await expect(leader.play()).resolves.toBeUndefined()
  })
})
