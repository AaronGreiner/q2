import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import GoalWindowRow from '~/components/goals/GoalWindowRow.vue'
import HistoryGrid from '~/components/goals/HistoryGrid.vue'
import type { Goal, GoalWindow } from '~/api/types'

/**
 * The two components the window model brought with it: the row somebody
 * delivers into, and the grid that shows what became of the ones before it.
 */
function window(overrides: Partial<GoalWindow> = {}): GoalWindow {
  return {
    id: 'window-1',
    startsOn: '2026-06-15',
    dueOn: '2026-06-21',
    dueAt: '2026-06-21T21:59:59+00:00',
    requiredProofs: 3,
    confirmedProofs: 1,
    remainingProofs: 2,
    status: 'Open',
    pendingProofId: null,
    acceptsProof: true,
    ...overrides,
  }
}

function goal(overrides: Partial<Goal> = {}): Goal {
  return {
    id: 'goal-1',
    title: 'Dreimal die Woche laufen',
    description: null,
    icon: 'medal',
    schedule: { kind: 'Times', everyDays: null, weekdays: [], times: 3, period: 'Week' },
    status: 'Active',
    isGroup: false,
    current: window(),
    streak: 4,
    windowsDone: 12,
    windowsMissed: 2,
    reminderAt: '18:00:00',
    targetDate: null,
    createdAt: '2026-05-15T09:00:00+00:00',
    participants: [],
    isOverdue: false,
    isMine: true,
    closedAt: null,
    risk: null,
    pause: null,
    remainingPauses: 2,
    ...overrides,
  }
}

describe('GoalWindowRow', () => {
  it('shows the title, the schedule and what the window still wants', async () => {
    const wrapper = await mountSuspended(GoalWindowRow, { props: { goal: goal() } })

    expect(wrapper.text()).toContain('Dreimal die Woche laufen')
    expect(wrapper.text()).toContain('3× pro Woche')
    expect(wrapper.get('[data-testid="window-remaining"]').text()).toBe('Noch 2 von 3')
    expect(wrapper.get('[data-testid="window-row"]').attributes()).toHaveProperty('data-q2-block')
  })

  it('offers a camera a keyboard can reach, named after what it delivers into', async () => {
    const wrapper = await mountSuspended(GoalWindowRow, { props: { goal: goal() } })
    const deliver = wrapper.get('[data-testid="window-deliver"]')

    expect(deliver.attributes('aria-label')).toBe('Beweis liefern: Dreimal die Woche laufen')

    await deliver.trigger('click')
    expect(wrapper.emitted('deliver')?.[0]).toEqual(['goal-1'])
  })

  /**
   * The state stage 4 added, and the one q2 could not previously express: not
   * done, and not still to do either. Offering the camera here would deliver a
   * second photograph the server refuses.
   */
  it('shows a photograph that is being looked at, and offers no camera for it', async () => {
    const wrapper = await mountSuspended(GoalWindowRow, {
      props: {
        goal: goal({
          current: window({ pendingProofId: 'proof-1', acceptsProof: false }),
        }),
      },
    })

    expect(wrapper.find('[data-testid="window-deliver"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="window-waiting"]').text()).toBe('Wird geprüft')
    expect(wrapper.get('[data-testid="window-state"]').attributes('aria-label')).toBe('Wird geprüft')
  })

  /**
   * A delivered window is a fact about the past, and there is nothing left to
   * hand in — so there is nothing to press.
   */
  it('offers nothing once the window is full', async () => {
    const wrapper = await mountSuspended(GoalWindowRow, {
      props: {
        goal: goal({
          current: window({ confirmedProofs: 3, remainingProofs: 0, status: 'Done', acceptsProof: false }),
        }),
      },
    })

    expect(wrapper.find('[data-testid="window-deliver"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="window-state"]').attributes('aria-label')).toBe('Bestätigt')
  })

  it('draws a ring only when the window wants more than one proof', async () => {
    const many = await mountSuspended(GoalWindowRow, { props: { goal: goal() } })
    expect(many.find('[data-testid="progress-ring"]').exists()).toBe(true)

    const one = await mountSuspended(GoalWindowRow, {
      props: {
        goal: goal({
          schedule: { kind: 'Interval', everyDays: 1, weekdays: [], times: null, period: null },
          current: window({ requiredProofs: 1, confirmedProofs: 0, remainingProofs: 1, startsOn: '2026-06-21' }),
        }),
      },
    })

    expect(one.find('[data-testid="progress-ring"]').exists()).toBe(false)
    expect(one.text()).toContain('Heute fällig')
  })
})

describe('HistoryGrid', () => {
  it('draws one square per window, oldest first', async () => {
    const wrapper = await mountSuspended(HistoryGrid, {
      props: {
        history: [
          window({ id: 'c', dueOn: '2026-06-21', status: 'Done' }),
          window({ id: 'b', dueOn: '2026-06-14', status: 'Missed' }),
          window({ id: 'a', dueOn: '2026-06-07', status: 'Done' }),
        ],
      },
    })

    const squares = wrapper.findAll('li')

    expect(squares).toHaveLength(3)
    expect(squares[0]!.text()).toContain('7.6.')
    expect(squares[2]!.text()).toContain('21.6.')
  })

  /** Status is never colour alone: every square says its outcome in words. */
  it('names every outcome for a screen reader', async () => {
    const wrapper = await mountSuspended(HistoryGrid, {
      props: { history: [window({ status: 'Missed' })] },
    })

    expect(wrapper.text()).toContain('Verpasst')
    expect(wrapper.get('[data-testid="history-grid"]').attributes()).toHaveProperty('data-q2-block')
  })

  it('stops at the limit rather than drawing a year of squares', async () => {
    const history = Array.from({ length: 40 }, (_, index) => window({ id: `w${index}`, status: 'Done' }))
    const wrapper = await mountSuspended(HistoryGrid, { props: { history, limit: 14 } })

    expect(wrapper.findAll('li')).toHaveLength(14)
  })
})
