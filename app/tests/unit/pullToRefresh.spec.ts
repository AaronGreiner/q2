import { describe, expect, it } from 'vitest'
import { PULL_MAX, PULL_THRESHOLD, isPullArmed, isPullGesture, pullDistance } from '~/utils/pullToRefresh'

/** The arithmetic behind pulling a list down to read it again (#19). */
describe('pullDistance', () => {
  it('follows half of the finger, so a pull has weight', () => {
    expect(pullDistance(40)).toBe(20)
    expect(pullDistance(PULL_THRESHOLD * 2)).toBe(PULL_THRESHOLD)
  })

  it('never goes further than its maximum', () => {
    expect(pullDistance(10_000)).toBe(PULL_MAX)
  })

  it('is nothing for a movement upwards, or none at all', () => {
    expect(pullDistance(0)).toBe(0)
    expect(pullDistance(-30)).toBe(0)
    expect(pullDistance(Number.NaN)).toBe(0)
  })
})

describe('isPullArmed', () => {
  it('refreshes from the threshold on, and not a pixel before', () => {
    expect(isPullArmed(PULL_THRESHOLD - 1)).toBe(false)
    expect(isPullArmed(PULL_THRESHOLD)).toBe(true)
    expect(isPullArmed(PULL_MAX)).toBe(true)
  })
})

describe('isPullGesture', () => {
  it('is a movement mostly downwards', () => {
    expect(isPullGesture(3, 20)).toBe(true)
  })

  it('is not a sideways swipe, even one that drifts down', () => {
    expect(isPullGesture(30, 10)).toBe(false)
    expect(isPullGesture(-30, 10)).toBe(false)
  })

  it('is not a scroll upwards', () => {
    expect(isPullGesture(0, -20)).toBe(false)
  })
})
