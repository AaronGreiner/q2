import { describe, expect, it } from 'vitest'
import {
  addDays,
  byDeadline,
  deadlineLeft,
  formatWeekdayDay,
  isStillOpen,
  localDay,
  nextUp,
  weekdayIndex,
  weekStates,
} from '~/utils/display'
import { firstWindowEnd } from '~/utils/firstWindow'
import { goalTemplates } from '~/utils/goalTemplates'
import { de, en } from '~/i18n/messages'
import { goalIcons } from '~/api/types'
import type { Goal, GoalWindow } from '~/api/types'

/**
 * What the start screen and the To-Dos say about time: how long is left, what
 * comes first, where today is in the week, and when a new goal's first window
 * ends.
 */

const noon = Date.parse('2026-09-24T12:00:00Z')

function window(overrides: Partial<GoalWindow> = {}): GoalWindow {
  return {
    id: 'w',
    startsOn: '2026-09-24',
    dueOn: '2026-09-24',
    dueAt: '2026-09-24T22:00:00Z',
    requiredProofs: 1,
    confirmedProofs: 0,
    remainingProofs: 1,
    status: 'Open',
    pendingProofId: null,
    acceptsProof: true,
    ...overrides,
  }
}

function goal(id: string, current: GoalWindow | null): Goal {
  return { id, title: id, current } as Goal
}

describe('deadlineLeft', () => {
  it('counts down in hours, then minutes, rounding up', () => {
    expect(deadlineLeft('2026-09-24T22:00:00Z', noon, de)).toBe('noch 10 Std.')
    expect(deadlineLeft('2026-09-24T12:25:00Z', noon, de)).toBe('noch 25 Min.')
    expect(deadlineLeft('2026-09-24T12:00:30Z', noon, de)).toBe('noch 1 Min.')
    expect(deadlineLeft('2026-09-24T22:00:00Z', noon, en)).toBe('10 h left')
  })

  it('switches to days past a day, and says when it is running out now', () => {
    expect(deadlineLeft('2026-09-27T12:00:00Z', noon, de)).toBe('noch 3 Tage')
    expect(deadlineLeft('2026-09-25T13:00:00Z', noon, de)).toBe('noch 1 Tag')
    expect(deadlineLeft('2026-09-24T11:00:00Z', noon, de)).toBe('läuft gerade ab')
  })

  it('says nothing about an instant it cannot read', () => {
    expect(deadlineLeft('not a date', noon, de)).toBeNull()
  })
})

describe('byDeadline and nextUp', () => {
  const late = goal('late', window({ dueAt: '2026-09-24T22:00:00Z' }))
  const early = goal('early', window({ dueAt: '2026-09-24T18:00:00Z' }))
  const waiting = goal('waiting', window({ dueAt: '2026-09-24T14:00:00Z', pendingProofId: 'p', acceptsProof: false }))
  const done = goal('done', window({ dueAt: '2026-09-24T13:00:00Z', remainingProofs: 0, confirmedProofs: 1, acceptsProof: false }))
  const week = goal('week', window({ dueOn: '2026-09-27', dueAt: '2026-09-27T22:00:00Z', requiredProofs: 3, remainingProofs: 2 }))

  it('puts what still wants a photograph first, each group by its deadline', () => {
    expect(byDeadline([done, week, waiting, late, early]).map(entry => entry.id))
      .toEqual(['early', 'late', 'week', 'waiting', 'done'])
  })

  it('knows what is still open', () => {
    expect(isStillOpen(early)).toBe(true)
    expect(isStillOpen(waiting)).toBe(false)
    expect(isStillOpen(done)).toBe(false)
    expect(isStillOpen(goal('none', null))).toBe(false)
  })

  it('picks the open window that closes first and takes a photograph now', () => {
    expect(nextUp([done, waiting, late, early])?.id).toBe('early')
    expect(nextUp([done, waiting])).toBeNull()
  })
})

describe('the week under the streak', () => {
  it('finds today Monday-first, in the reader\'s zone', () => {
    // A Thursday at noon in UTC.
    expect(weekdayIndex(noon)).toBe(3)

    // Sunday late evening in UTC is already Monday two hours east.
    const sundayNight = Date.parse('2026-09-27T23:00:00Z')
    expect(weekdayIndex(sundayNight)).toBe(6)
    expect(weekdayIndex(sundayNight, 120)).toBe(0)
  })

  it('marks what was kept, today, what was not, and what is still to come', () => {
    expect(weekStates([true, false, true, false, false, false, false], 3))
      .toEqual(['done', 'past', 'done', 'today', 'future', 'future', 'future'])
    expect(weekStates([false, false, false, true, false, false, false], 3)[3]).toBe('todayDone')
  })
})

describe('days', () => {
  it('writes a day with its weekday', () => {
    expect(formatWeekdayDay('2026-09-26', de)).toBe('Sa, 26.9.')
    expect(formatWeekdayDay('2026-09-26', en)).toBe('Sat, 26.9.')
  })

  it('reads the local day and moves by whole days across months', () => {
    expect(localDay(Date.parse('2026-09-30T23:30:00Z'))).toBe('2026-09-30')
    expect(localDay(Date.parse('2026-09-30T23:30:00Z'), 60)).toBe('2026-10-01')
    expect(addDays('2026-09-30', 1)).toBe('2026-10-01')
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28')
  })
})

describe('firstWindowEnd', () => {
  // 2026-09-24 is a Thursday.
  const today = '2026-09-24'

  it('starts an interval and a one-off today', () => {
    expect(firstWindowEnd({ kind: 'Interval', everyDays: 3 }, today)).toBe(today)
    expect(firstWindowEnd({ kind: 'Once' }, today)).toBe(today)
  })

  it('waits for the next chosen weekday, today included', () => {
    expect(firstWindowEnd({ kind: 'Weekdays', weekdays: ['Thursday'] }, today)).toBe(today)
    expect(firstWindowEnd({ kind: 'Weekdays', weekdays: ['Monday'] }, today)).toBe('2026-09-28')
    expect(firstWindowEnd({ kind: 'Weekdays', weekdays: ['Tuesday', 'Saturday'] }, today)).toBe('2026-09-26')
  })

  it('runs a quota to the end of its week or month', () => {
    expect(firstWindowEnd({ kind: 'Times', times: 3, period: 'Week' }, today)).toBe('2026-09-27')
    expect(firstWindowEnd({ kind: 'Times', times: 3, period: 'Month' }, today)).toBe('2026-09-30')
    expect(firstWindowEnd({ kind: 'Times', times: 1, period: 'Week' }, '2026-09-27')).toBe('2026-09-27')
  })
})

describe('goalTemplates', () => {
  it('only offers icons the server accepts, and words in both languages', () => {
    for (const template of goalTemplates) {
      expect(goalIcons).toContain(template.icon)
      expect(de.create.templates[template.key]).toBeTruthy()
      expect(en.create.templates[template.key]).toBeTruthy()
    }
  })
})
