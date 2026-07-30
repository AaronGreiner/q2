import { describe, expect, it } from 'vitest'
import {
  clampProgress,
  daysUntil,
  describeParticipants,
  describeProgress,
  describeTargetDate,
  formatDate,
  goalStatusPresentation,
} from '~/utils/goalDisplay'

/**
 * Presentation logic. Every function takes "today" as an argument, so none of
 * these tests depend on when they run or on the machine's time zone.
 */
const today = new Date('2026-06-15T09:00:00Z')

describe('clampProgress', () => {
  it.each([
    [0, 0],
    [50, 50],
    [100, 100],
    [-10, 0],
    [150, 100],
    [49.6, 50],
    [Number.NaN, 0],
    [Number.POSITIVE_INFINITY, 0],
  ])('maps %s to %s', (input, expected) => {
    expect(clampProgress(input)).toBe(expected)
  })
})

describe('describeProgress', () => {
  it('reads as a sentence for a screen reader', () => {
    expect(describeProgress(45)).toBe('45% complete')
  })

  it('clamps before describing, so a bad value never reads as nonsense', () => {
    expect(describeProgress(500)).toBe('100% complete')
  })
})

describe('goalStatusPresentation', () => {
  it('gives every status a distinct label and icon', () => {
    const active = goalStatusPresentation('Active')
    const completed = goalStatusPresentation('Completed')
    const archived = goalStatusPresentation('Archived')

    expect(new Set([active.label, completed.label, archived.label]).size).toBe(3)
    expect(new Set([active.icon, completed.icon, archived.icon]).size).toBe(3)
  })

  it('never relies on colour alone', () => {
    for (const status of ['Active', 'Completed', 'Archived'] as const) {
      expect(goalStatusPresentation(status).label).not.toBe('')
    }
  })
})

describe('daysUntil', () => {
  it('counts forwards', () => {
    expect(daysUntil('2026-06-25', today)).toBe(10)
  })

  it('counts backwards for past dates', () => {
    expect(daysUntil('2026-06-05', today)).toBe(-10)
  })

  it('is zero on the day itself, regardless of the time of day', () => {
    expect(daysUntil('2026-06-15', today)).toBe(0)
    expect(daysUntil('2026-06-15', new Date('2026-06-15T23:59:00Z'))).toBe(0)
  })

  it('returns null without a target date', () => {
    expect(daysUntil(null, today)).toBeNull()
  })

  it('returns null for an unparseable date rather than NaN', () => {
    expect(daysUntil('not-a-date', today)).toBeNull()
  })
})

describe('describeTargetDate', () => {
  it('says nothing when there is no target date', () => {
    expect(describeTargetDate({ targetDate: null, isOverdue: false }, today)).toBeNull()
  })

  it('phrases today and tomorrow in words', () => {
    expect(describeTargetDate({ targetDate: '2026-06-15', isOverdue: false }, today)).toBe('Due today')
    expect(describeTargetDate({ targetDate: '2026-06-16', isOverdue: false }, today)).toBe('Due tomorrow')
  })

  it('counts remaining days', () => {
    expect(describeTargetDate({ targetDate: '2026-06-22', isOverdue: false }, today)).toBe('Due in 7 days')
  })

  it('uses the server-derived overdue flag rather than recomputing it', () => {
    // The browser's clock is not the authority on whether something is late.
    expect(describeTargetDate({ targetDate: '2026-06-14', isOverdue: true }, today)).toBe('Overdue by 1 day')
    expect(describeTargetDate({ targetDate: '2026-06-08', isOverdue: true }, today)).toBe('Overdue by 7 days')
  })

  it('does not claim a goal is overdue when the server says it is not', () => {
    // A completed goal with a past target date.
    expect(describeTargetDate({ targetDate: '2026-06-08', isOverdue: false }, today))
      .toBe('Target date was 8 Jun 2026')
  })
})

describe('formatDate', () => {
  it('formats independently of the local time zone', () => {
    expect(formatDate('2026-09-12')).toBe('12 Sep 2026')
  })

  it('returns the input unchanged when it cannot be parsed', () => {
    expect(formatDate('nonsense')).toBe('nonsense')
  })
})

describe('describeParticipants', () => {
  it('says nothing for nobody', () => {
    expect(describeParticipants([])).toBeNull()
  })

  it('names one and two people in full', () => {
    expect(describeParticipants(['Robin Sample'])).toBe('Robin Sample')
    expect(describeParticipants(['Robin Sample', 'Kim Example'])).toBe('Robin Sample and Kim Example')
  })

  it('summarises larger groups', () => {
    expect(describeParticipants(['Robin Sample', 'Kim Example', 'Alex Placeholder']))
      .toBe('Robin Sample, Kim Example and 1 other')

    expect(describeParticipants(['Robin Sample', 'Kim Example', 'Alex Placeholder', 'Sam Fixture']))
      .toBe('Robin Sample, Kim Example and 2 others')
  })
})
