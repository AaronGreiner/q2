import { describe, expect, it } from 'vitest'
import type { Goal, GoalEvent, GoalWindow } from '~/api/types'
import { de } from '~/i18n/messages'
import { goalEventIcon, goalEventText } from '~/utils/chatTimeline'
import { goalScheduleLabel, nextUp, runDays, windowLabel, windowOutcome } from '~/utils/display'
import { firstWindowEnd } from '~/utils/firstWindow'
import { proofSourceOf } from '~/utils/proofSource'

/**
 * The words a Don't is shown with (docs/adr/0037-donts.md): its limit, what
 * its period has used, a relapse said as one, and its run.
 */
function window(overrides: Partial<GoalWindow> = {}): GoalWindow {
  return {
    id: 'window-1',
    startsOn: '2026-06-15',
    dueOn: '2026-06-21',
    dueAt: '2026-06-21T21:59:59+00:00',
    requiredProofs: 0,
    confirmedProofs: 0,
    remainingProofs: 0,
    status: 'Open',
    pendingProofId: null,
    acceptsProof: false,
    limit: 3,
    uses: 1,
    ...overrides,
  }
}

function dont(overrides: Partial<Goal> = {}): Goal {
  return {
    id: 'dont-1',
    title: 'Höchstens 3 Bier',
    description: null,
    icon: 'flame',
    schedule: { kind: 'Limit', everyDays: null, weekdays: [], times: null, period: 'Week' },
    status: 'Active',
    isGroup: false,
    kind: 'Dont',
    limit: 3,
    proofMode: 'HonestOnly',
    current: window(),
    streak: 2,
    windowsDone: 2,
    windowsMissed: 0,
    windowsRelapsed: 1,
    runStartedOn: '2026-06-10',
    reminderAt: null,
    targetDate: null,
    createdAt: '2026-05-15T09:00:00+00:00',
    closedAt: null,
    participants: [],
    isOverdue: false,
    isMine: true,
    risk: null,
    pause: null,
    remainingPauses: 2,
    ...overrides,
  }
}

function event(overrides: Partial<GoalEvent>): GoalEvent {
  return {
    key: 'event-1',
    kind: 'UseLogged',
    at: '2026-06-15T20:00:00+00:00',
    actorName: null,
    isMine: true,
    streak: null,
    confirmedProofs: null,
    requiredProofs: null,
    until: null,
    proof: null,
    day: null,
    uses: null,
    limit: null,
    imageId: null,
    ...overrides,
  }
}

describe('a Don\'t, in words', () => {
  it('says its limit, and "never" for none', () => {
    expect(goalScheduleLabel(dont(), de)).toBe('Höchstens 3× pro Woche')
    expect(goalScheduleLabel(dont({ limit: 0, schedule: { ...dont().schedule, period: 'Day' } }), de)).toBe('Nie')
  })

  it('says what the period has used, or that it relapsed', () => {
    expect(windowLabel(window(), de)).toBe('1 von 3 genutzt')
    expect(windowLabel(window({ limit: 0, uses: 0 }), de)).toBe('Bisher durchgehalten')
    expect(windowLabel(window({ status: 'Relapse', uses: 4 }), de)).toBe('Rückfall')
  })

  it('calls a kept period held, and a relapse a relapse, never a miss', () => {
    expect(windowOutcome(window({ status: 'Done' }), de).label).toBe('Durchgehalten')
    expect(windowOutcome(window({ status: 'Relapse' }), de)).toMatchObject({ label: 'Rückfall', icon: 'i-lucide-rotate-ccw' })

    // A Do's window is still "Geschafft".
    expect(windowOutcome(window({ status: 'Done', limit: null }), de).label).toBe('Geschafft')
  })

  it('counts its run in whole days, and the day of a relapse as tomorrow', () => {
    expect(runDays('2026-06-10', '2026-06-22')).toBe(12)
    expect(runDays('2026-06-22', '2026-06-22')).toBe(0)
    expect(de.dont.run(12)).toBe('Seit 12 Tagen')

    // The day of a relapse: the run starts tomorrow, not today.
    expect(runDays('2026-06-23', '2026-06-22')).toBe(-1)
    expect(de.dont.run(-1)).toBe('Ab morgen')
  })

  it('is never what the start screen puts first', () => {
    const proofAtEnd = dont({ proofMode: 'ProofAtEnd', current: window({ requiredProofs: 1, remainingProofs: 1, acceptsProof: true }) })

    expect(nextUp([proofAtEnd])).toBeNull()
  })

  it('ends its first window with its period', () => {
    // 2026-06-17 is a Wednesday.
    expect(firstWindowEnd({ kind: 'Limit', period: 'Day' }, '2026-06-17')).toBe('2026-06-17')
    expect(firstWindowEnd({ kind: 'Limit', period: 'Week' }, '2026-06-17')).toBe('2026-06-21')
    expect(firstWindowEnd({ kind: 'Limit', period: 'Month' }, '2026-06-17')).toBe('2026-06-30')
  })
})

describe('a Don\'t, in its conversation', () => {
  it('states every logged use with its count against the limit', () => {
    expect(goalEventText(event({ uses: 2, limit: 3 }), de)).toBe('Du hast eingetragen · 2 von 3')
    expect(goalEventText(event({ uses: 1, limit: 0, isMine: false, actorName: 'Jonas' }), de)).toBe('Jonas hat eingetragen · über dem Limit')
    expect(goalEventText(event({ uses: 4, limit: 3 }), de)).toBe('Du hast eingetragen · über dem Limit')
    expect(goalEventIcon('UseLogged')).toBe('i-lucide-plus')
  })

  it('says a relapse and the streak it held, on its day', () => {
    expect(goalEventText(event({ kind: 'WindowRelapse', streak: 4, day: '2026-06-21' }), de))
      .toBe('Rückfall · Streak hält bei 4 · 21.6.')
    expect(goalEventText(event({ kind: 'WindowRelapse', streak: 0 }), de)).toBe('Rückfall')
    expect(goalEventIcon('WindowRelapse')).toBe('i-lucide-rotate-ccw')
  })

  it('calls a kept period held, where a Do\'s is done', () => {
    expect(goalEventText(event({ kind: 'WindowDone', streak: 3, limit: 0 }), de)).toBe('Durchgehalten · Streak 3')
    expect(goalEventText(event({ kind: 'WindowDone', streak: 3 }), de)).toBe('Geschafft · Streak 3')
  })
})

describe('the route a picture came by', () => {
  it('is live from the shutter and a screenshot from the phone', () => {
    expect(proofSourceOf('camera')).toBe('Live')
    expect(proofSourceOf('library')).toBe('Screenshot')
  })
})
