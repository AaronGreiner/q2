import { describe, expect, it } from 'vitest'
import {
  activitySentence,
  clampProgress,
  formatAmount,
  formatChatTime,
  formatClock,
  formatMeasure,
  formatRelativeTime,
  goalIconName,
  goalStatusPresentation,
  goalSubtitle,
} from '~/utils/display'
import { de, en } from '~/i18n/messages'
import type { Activity } from '~/api/types'

/**
 * The presentation helpers.
 *
 * Every one of them takes its clock, its time zone and its language as
 * arguments, which is exactly why they can be tested like this — and why the
 * server-rendered HTML and the browser's agree.
 */

// 2026-06-17 is a Wednesday. 09:00 UTC.
const now = Date.parse('2026-06-17T09:00:00Z')

describe('clampProgress', () => {
  it('rounds to a whole percent', () => {
    expect(clampProgress(66.6)).toBe(67)
  })

  it.each([[-10, 0], [0, 0], [50, 50], [100, 100], [140, 100]])(
    'clamps %i to %i so a bad value cannot render a broken bar',
    (input, expected) => {
      expect(clampProgress(input)).toBe(expected)
    },
  )

  it('treats a value that is not a number as no progress', () => {
    expect(clampProgress(Number.NaN)).toBe(0)
  })
})

describe('goalStatusPresentation', () => {
  it('gives every status a label and an icon, so colour is never the only signal', () => {
    for (const status of ['Active', 'Completed', 'Archived'] as const) {
      const presentation = goalStatusPresentation(status, de)

      expect(presentation.label).not.toBe('')
      expect(presentation.icon).toMatch(/^i-lucide-/)
    }
  })

  it('speaks the language it is given', () => {
    expect(goalStatusPresentation('Completed', de).label).toBe('Abgeschlossen')
    expect(goalStatusPresentation('Completed', en).label).toBe('Completed')
  })
})

describe('goalIconName', () => {
  it('maps a server icon onto the bundled icon set', () => {
    expect(goalIconName('medal')).toBe('i-lucide-medal')
  })
})

describe('formatClock', () => {
  it('shows a reminder time as written, whatever the reader\'s zone', () => {
    // A TimeOnly is wall-clock already; shifting it would move somebody's
    // seven-in-the-morning run.
    expect(formatClock('07:00:00', 120)).toBe('07:00')
  })

  it('pads a single-digit hour', () => {
    expect(formatClock('7:5:00')).toBe('07:05')
  })

  it('shows an instant in UTC when no offset is known', () => {
    expect(formatClock('2026-06-17T09:05:00+00:00')).toBe('09:05')
  })

  it('shifts an instant into the reader\'s zone once it is known', () => {
    // This is what turns the server-rendered 17:55 into a local 19:55 after
    // hydration.
    expect(formatClock('2026-06-17T17:55:00+00:00', 120)).toBe('19:55')
  })

  it('returns null rather than a broken string for nonsense', () => {
    expect(formatClock(null)).toBeNull()
    expect(formatClock('not a time')).toBeNull()
  })
})

describe('formatRelativeTime', () => {
  it.each([
    ['2026-06-17T09:00:00Z', 'gerade eben'],
    ['2026-06-17T08:48:00Z', 'vor 12 Min'],
    ['2026-06-17T06:00:00Z', 'vor 3 Std'],
    ['2026-06-16T09:00:00Z', 'gestern'],
    ['2026-06-13T09:00:00Z', 'vor 4 Tagen'],
  ])('reads %s as "%s"', (iso, expected) => {
    expect(formatRelativeTime(iso, now, de)).toBe(expected)
  })

  it('never reports a negative age when a clock is slightly ahead', () => {
    expect(formatRelativeTime('2026-06-17T09:00:30Z', now, de)).toBe('gerade eben')
  })

  it('translates', () => {
    expect(formatRelativeTime('2026-06-16T09:00:00Z', now, en)).toBe('yesterday')
  })
})

describe('formatChatTime', () => {
  it('shows the clock for today', () => {
    expect(formatChatTime('2026-06-17T08:30:00Z', now, de)).toBe('08:30')
  })

  it('says "gestern" for yesterday', () => {
    expect(formatChatTime('2026-06-16T20:00:00Z', now, de)).toBe('gestern')
  })

  it('names the weekday within the week', () => {
    expect(formatChatTime('2026-06-15T20:00:00Z', now, de)).toBe('Mo')
  })

  it('falls back to a date beyond a week', () => {
    expect(formatChatTime('2026-06-01T20:00:00Z', now, de)).toBe('1.6.')
  })

  it('decides "today" in the reader\'s zone, not the server\'s', () => {
    // 23:30 UTC is already the next day in Berlin, and the row has to say so.
    expect(formatChatTime('2026-06-16T23:30:00Z', now, de, 120)).toBe('01:30')
  })

  it('returns an empty string for a conversation with no messages', () => {
    expect(formatChatTime(null, now, de)).toBe('')
  })
})

describe('formatAmount and formatMeasure', () => {
  it('writes a decimal the way the language does', () => {
    expect(formatAmount(1.2, ',')).toBe('1,2')
    expect(formatAmount(1.2, '.')).toBe('1.2')
  })

  it('leaves a whole number whole', () => {
    expect(formatAmount(2, ',')).toBe('2')
  })

  it('builds the amount under a measurable task', () => {
    const task = { measuredValue: 1.2, targetValue: 2, measureUnit: 'L' }

    expect(formatMeasure(task, ',')).toBe('1,2 / 2 L')
  })

  it('starts at zero when nothing has been measured yet', () => {
    expect(formatMeasure({ measuredValue: null, targetValue: 2, measureUnit: 'L' }, ',')).toBe('0 / 2 L')
  })

  it('is null for a task that is a plain tick', () => {
    expect(formatMeasure({ measuredValue: null, targetValue: null, measureUnit: null }, ',')).toBeNull()
  })
})

describe('activitySentence', () => {
  function activity(overrides: Partial<Activity>): Activity {
    return {
      id: 'a',
      actor: {
        id: 'p',
        displayName: 'Jonas Weber',
        handle: '@jonas.w',
        initials: 'JW',
        avatarColor: '#4f46e5',
        isOnline: true,
      },
      kind: 'TaskCompleted',
      subject: 'Joggen 5 km',
      amount: null,
      kudosCount: 8,
      hasMyKudos: false,
      occurredAt: '2026-06-17T08:48:00+00:00',
      ...overrides,
    }
  }

  it('composes the sentence from the parts the server sent', () => {
    expect(activitySentence(activity({}), de)).toBe('hat „Joggen 5 km“ abgeschlossen')
    expect(activitySentence(activity({}), en)).toBe('completed “Joggen 5 km”')
  })

  it('uses the amount for a streak', () => {
    const entry = activity({ kind: 'StreakReached', subject: null, amount: 7 })

    expect(activitySentence(entry, de)).toContain('7')
    expect(activitySentence(entry, en)).toContain('7')
  })

  it('covers every kind the contract can produce', () => {
    for (const kind of ['TaskCompleted', 'StreakReached', 'GoalProgress', 'GoalCreated'] as const) {
      expect(activitySentence(activity({ kind, amount: 5 }), de)).not.toBe('')
    }
  })
})

describe('goalSubtitle', () => {
  it('counts steps when there are steps to count', () => {
    expect(goalSubtitle({ completedSteps: 14, totalSteps: 21, progressPercent: 67 }, de))
      .toBe('14 von 21 Schritten')
  })

  it('falls back to the percentage for a one-step goal', () => {
    expect(goalSubtitle({ completedSteps: 0, totalSteps: 1, progressPercent: 0 }, de))
      .toContain('0')
  })
})
