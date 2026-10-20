import { describe, expect, it } from 'vitest'
import type { GoalChange, GoalEvent } from '~/api/types'
import { de, en } from '~/i18n/messages'
import { goalEventIcon, goalEventText } from '~/utils/chatTimeline'
import { goalChangeItems, goalChangeTiming, objectionDeadline } from '~/utils/goalChange'

/**
 * An edit of a running goal, in words (docs/adr/0044-editing-a-running-goal.md):
 * what changed from what to what, what applied at once and what waits, and
 * until when the checkers can object.
 */
function change(overrides: Partial<GoalChange> = {}): GoalChange {
  return {
    id: 'change-1',
    status: 'Pending',
    createdAt: '2026-06-15T09:00:00+00:00',
    objectable: true,
    objectionEndsAt: '2026-06-16T09:00:00+00:00',
    appliesOn: '2026-06-17',
    resolvedAt: null,
    titleFrom: null,
    titleTo: null,
    categoryFrom: null,
    categoryTo: null,
    descriptionChanged: false,
    descriptionTo: null,
    scheduleFrom: null,
    scheduleTo: null,
    limitFrom: null,
    limitTo: null,
    proofModeFrom: null,
    proofModeTo: null,
    targetDateFrom: null,
    targetDateTo: null,
    checkersAdded: [],
    checkersRemoved: [],
    vetoCount: 0,
    vetoesRequired: 2,
    vetoedByMe: false,
    canVeto: false,
    ...overrides,
  }
}

const lena = { id: 'person-lena', displayName: 'Lena', handle: '@lena', initials: 'L', avatarColor: '#4f46e5', isOnline: false, avatarImageId: null }

describe('goalChangeItems', () => {
  it('says what changed from what to what, and what applied at once', () => {
    const items = goalChangeItems(change({
      titleFrom: 'Laufen',
      titleTo: 'Joggen',
      scheduleFrom: { kind: 'Times', everyDays: null, weekdays: [], times: 3, period: 'Week' },
      scheduleTo: { kind: 'Times', everyDays: null, weekdays: [], times: 2, period: 'Week' },
      checkersAdded: [lena],
    }), de)

    expect(items.map(item => [item.text, item.now])).toEqual([
      ['Titel: „Laufen“ → „Joggen“', true],
      ['Rhythmus: 3× pro Woche → 2× pro Woche', false],
      ['Lena prüft jetzt mit', true],
    ])
  })

  it('words a Don\'t by its limit, its period and how it is checked', () => {
    const items = goalChangeItems(change({
      scheduleFrom: { kind: 'Limit', everyDays: null, weekdays: [], times: null, period: 'Week' },
      scheduleTo: { kind: 'Limit', everyDays: null, weekdays: [], times: null, period: 'Month' },
      limitFrom: 0,
      limitTo: 2,
      proofModeFrom: 'SpotCheck',
      proofModeTo: 'HonestOnly',
    }), de).map(item => item.text)

    expect(items).toEqual([
      'Gezählt pro Woche → pro Monat',
      'Limit: nie → 2×',
      'Prüfung: Stichprobe → Nur ehrlich',
    ])
  })

  it('moves an earlier day at once and a later one only with the change', () => {
    const earlier = goalChangeItems(change({ targetDateFrom: '2026-06-20', targetDateTo: '2026-06-18' }), de)
    const later = goalChangeItems(change({ targetDateFrom: '2026-06-20', targetDateTo: '2026-06-25' }), de)

    expect(earlier[0]).toMatchObject({ text: 'Termin: 20.6. → 18.6.', now: true })
    expect(later[0]).toMatchObject({ text: 'Termin: 20.6. → 25.6.', now: false })
  })

  it('names who stops checking, and says a description changed without quoting it', () => {
    const items = goalChangeItems(change({ checkersRemoved: [lena], descriptionChanged: true, descriptionTo: 'Mindestens 5 km' }), en)

    expect(items.map(item => item.text)).toEqual(['New description', 'Lena stops checking it'])
  })
})

describe('the timing of a change', () => {
  it('says from which day it applies, and until when it can be objected to', () => {
    expect(goalChangeTiming(change(), de)).toBe('Gilt ab Mi, 17.6.')
    expect(goalChangeTiming(change({ appliesOn: null }), de)).toBe('Gilt ab dem nächsten Fenster')
    expect(objectionDeadline(change(), de, 120)).toBe('Di, 16.6., 11:00 Uhr')
  })

  it('has no objection deadline for a change that cannot be objected to', () => {
    expect(objectionDeadline(change({ objectable: false, objectionEndsAt: null }), de)).toBeNull()
  })
})

describe('the lines in the conversation', () => {
  function event(kind: GoalEvent['kind'], overrides: Partial<GoalEvent> = {}): GoalEvent {
    return {
      key: `${kind}-1`,
      kind,
      at: '2026-06-15T09:00:00+00:00',
      actorName: 'Mara',
      isMine: false,
      streak: null,
      confirmedProofs: null,
      requiredProofs: null,
      until: null,
      proof: null,
      day: null,
      change: change(),
      ...overrides,
    }
  }

  it('words every kind of change in both languages', () => {
    const kinds = ['Edited', 'ChangeApplied', 'ChangeOverturned', 'ChangeWithdrawn'] as const

    for (const kind of kinds) {
      expect(goalEventText(event(kind), de)).not.toBe('')
      expect(goalEventText(event(kind), en)).not.toBe(goalEventText(event(kind), de))
      expect(goalEventIcon(kind)).toMatch(/^i-lucide-/)
    }
  })

  it('names who changed it, and nobody for what objections lifted', () => {
    expect(goalEventText(event('Edited'), de)).toBe('Mara hat es geändert.')
    expect(goalEventText(event('Edited', { isMine: true }), de)).toBe('Du hast es geändert.')
    expect(goalEventText(event('ChangeOverturned', { actorName: null }), de)).toBe('Die Änderung wurde durch Einspruch aufgehoben.')
    expect(goalEventText(event('ChangeApplied', { actorName: null, day: '2026-06-17' }), de)).toBe('Die Änderung gilt ab 17.6.')
  })
})
