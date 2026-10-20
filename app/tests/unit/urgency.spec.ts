import { describe, expect, it } from 'vitest'
import {
  addDays,
  byDeadline,
  deadlineLeft,
  formatWeekdayDay,
  goalListSections,
  isStillOpen,
  localDay,
  nextUp,
} from '~/utils/display'
import { firstWindow, firstWindowEnd, prorate } from '~/utils/firstWindow'
import { dontTemplates, goalTemplates } from '~/utils/goalTemplates'
import { de, en } from '~/i18n/messages'
import { goalCategories } from '~/api/types'
import type { Goal, GoalWindow } from '~/api/types'

/**
 * What the start screen and the Dos say about time: how long is left, what
 * comes first, and when a new goal's first window ends.
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
  return { id, title: id, current, isMine: true } as Goal
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

  it('never picks a friend\'s goal, even when its window closes first (#78)', () => {
    // A friend's window takes a photograph too — only its owner may hand one in.
    const friends = { ...goal('friends', window({ dueAt: '2026-09-24T13:00:00Z' })), isMine: false } as Goal

    expect(nextUp([late, friends, early])?.id).toBe('early')
    expect(nextUp([friends])).toBeNull()
  })
})

/**
 * The Dos screen's two segments and their two sections (#77): yours apart from
 * the ones you only check, and in each what is due today first.
 */
describe('goalListSections', () => {
  function listed(id: string, overrides: Partial<Goal> = {}): Goal {
    return { ...goal(id, window()), kind: 'Do', isMine: true, ...overrides } as Goal
  }

  const ownLate = listed('own-late', { current: window({ dueAt: '2026-09-24T22:00:00Z' }) })
  const ownEarly = listed('own-early', { current: window({ dueAt: '2026-09-24T18:00:00Z' }) })
  const ownLater = listed('own-later')
  const friends = listed('friends', { isMine: false })
  const friendsLater = listed('friends-later', { isMine: false })
  const ownDont = listed('own-dont', { kind: 'Dont' })

  const goals = [ownLater, ownLate, friendsLater, friends, ownEarly, ownDont]
  const due = [ownLate, friends, ownEarly, ownDont]

  it('keeps yours apart from your friends\', and one kind from the other', () => {
    const { mine, friends: checked } = goalListSections(goals, due, 'Do')

    expect(mine.map(row => row.goal.id)).toEqual(['own-early', 'own-late', 'own-later'])
    expect(checked.map(row => row.goal.id)).toEqual(['friends', 'friends-later'])

    const donts = goalListSections(goals, due, 'Dont')
    expect(donts.mine.map(row => row.goal.id)).toEqual(['own-dont'])
    expect(donts.friends).toEqual([])
  })

  it('puts what covers today first, by deadline, and marks only those as due', () => {
    const { mine, friends: checked } = goalListSections(goals, due, 'Do')

    expect(mine.map(row => row.isDue)).toEqual([true, true, false])
    expect(checked.map(row => row.isDue)).toEqual([true, false])
  })

  it('does not take a goal for due because its window is open', () => {
    // A window that has not started yet is open on the server and takes a
    // photograph; only the server's list of what covers today says it is due.
    const { mine } = goalListSections([ownLater], [], 'Do')

    expect(mine).toEqual([{ goal: ownLater, isDue: false }])
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

  // The day a one-off is given is the day its only window ends (#94); every
  // other kind derives its own and ignores it.
  it('ends a one-off on the day it was given', () => {
    expect(firstWindowEnd({ kind: 'Once' }, today, '2026-10-31')).toBe('2026-10-31')
    expect(firstWindowEnd({ kind: 'Interval', everyDays: 1 }, today, '2026-10-31')).toBe(today)
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
  it('only offers categories the server knows, and words in both languages', () => {
    for (const template of [...goalTemplates, ...dontTemplates]) {
      expect(goalCategories).toContain(template.category)
    }

    for (const template of goalTemplates) {
      expect(de.create.templates[template.key]).toBeTruthy()
      expect(en.create.templates[template.key]).toBeTruthy()
    }
  })
})

describe('firstWindow (#96)', () => {
  // A Monday.
  const monday = '2026-10-19'

  it('cuts a quota that starts late in its week and asks for its share', () => {
    const thursday = firstWindow({ kind: 'Times', times: 3, period: 'Week' }, '2026-10-22')
    expect(thursday).toEqual({ start: '2026-10-22', end: '2026-10-25', days: 4, fullDays: 7 })
    expect(prorate(3, thursday, 1)).toBe(2)

    const sunday = firstWindow({ kind: 'Times', times: 3, period: 'Week' }, '2026-10-25')
    expect(prorate(3, sunday, 1)).toBe(1)

    // Started on the Monday: the whole week, all three.
    expect(prorate(3, firstWindow({ kind: 'Times', times: 3, period: 'Week' }, monday), 1)).toBe(3)
  })

  it('cuts a month the same way, and a Don\'t\'s limit may come to nothing', () => {
    const late = firstWindow({ kind: 'Times', times: 2, period: 'Month' }, '2026-10-30')
    expect(late).toMatchObject({ start: '2026-10-30', end: '2026-10-31', days: 2, fullDays: 31 })
    expect(prorate(2, late, 1)).toBe(1)

    const never = firstWindow({ kind: 'Limit', period: 'Week' }, '2026-10-25')
    expect(prorate(0, never, 0)).toBe(0)
    expect(prorate(3, never, 0)).toBe(1)
  })

  it('starts a daily goal on the day it is told to', () => {
    expect(firstWindow({ kind: 'Interval', everyDays: 1 }, '2026-10-20')).toEqual({ start: '2026-10-20', end: '2026-10-20', days: 1, fullDays: 1 })
  })
})
