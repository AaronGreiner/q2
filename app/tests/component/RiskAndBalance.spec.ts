import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import ActivityRow from '~/components/social/ActivityRow.vue'
import BalanceCard from '~/components/profile/BalanceCard.vue'
import RiskCard from '~/components/goals/RiskCard.vue'
import type { Activity, Goal, GoalWindow, SharedGoal } from '~/api/types'

/**
 * The shame half, on screen.
 *
 * Every assertion here is about restraint rather than about layout. A warning
 * that says too much, or a record that offers a way to pile on, is the one
 * thing this product is not allowed to be — so the tests are mostly about what
 * is *absent*.
 */
function window(overrides: Partial<GoalWindow> = {}): GoalWindow {
  return {
    id: 'window-1',
    startsOn: '2026-06-15',
    dueOn: '2026-06-15',
    dueAt: '2026-06-15T21:59:59+00:00',
    requiredProofs: 1,
    confirmedProofs: 0,
    remainingProofs: 1,
    status: 'Open',
    pendingProofId: null,
    acceptsProof: true,
    ...overrides,
  }
}

function goal(overrides: Partial<Goal> = {}): Goal {
  return {
    id: 'goal-1',
    title: 'Jeden Tag laufen',
    description: null,
    category: 'Sport',
    icon: 'dumbbell',
    schedule: { kind: 'Interval', everyDays: 1, weekdays: [], times: null, period: null },
    status: 'Active',
    isGroup: false,
    current: window(),
    streak: 4,
    windowsDone: 12,
    windowsMissed: 2,
    reminderAt: null,
    targetDate: null,
    createdAt: '2026-05-15T09:00:00+00:00',
    participants: [],
    isOverdue: false,
    isMine: true,
    closedAt: null,
    risk: { reason: 'LastDay', missingProofs: 1, requiredProofs: 1, remainingDays: 1 },
    pause: null,
    remainingPauses: 2,
    ...overrides,
  }
}

function activity(overrides: Partial<Activity> = {}): Activity {
  return {
    id: 'activity-1',
    actor: {
      id: 'person-1',
      displayName: 'Jonas Weber',
      handle: '@jonas.w',
      initials: 'JW',
      avatarColor: '#4f46e5',
      isOnline: true,
      avatarImageId: null,
    },
    kind: 'TaskCompleted',
    subject: 'Jeden Morgen joggen',
    amount: null,
    kudosCount: 3,
    hasMyKudos: false,
    occurredAt: '2026-06-15T08:00:00+00:00',
    goalId: null,
    ...overrides,
  }
}

describe('RiskCard', () => {
  it('says what is missing and how long there is left', async () => {
    const wrapper = await mountSuspended(RiskCard, { props: { goal: goal() } })

    expect(wrapper.text()).toContain('Jeden Tag laufen')
    expect(wrapper.get('[data-testid="risk-sentence"]').text()).toBe('Heute ist der letzte Tag.')
  })

  it('counts what is outstanding on a quota rather than what was asked for', async () => {
    const wrapper = await mountSuspended(RiskCard, {
      props: {
        goal: goal({
          risk: { reason: 'Tight', missingProofs: 2, requiredProofs: 3, remainingDays: 2 },
        }),
      },
    })

    expect(wrapper.get('[data-testid="risk-sentence"]').text()).toBe('2 Nachweise fehlen in 2 Tagen.')
  })

  /**
   * The numbers are unpleasant enough on their own. Anything about a past
   * record here would be piling on, and at this point nothing has gone wrong.
   */
  it('says nothing about what was missed before', async () => {
    const wrapper = await mountSuspended(RiskCard, {
      props: { goal: goal({ windowsMissed: 9 }) },
    })

    expect(wrapper.text()).not.toContain('9')
  })

  it('still offers the camera, because that is the thing to do', async () => {
    const wrapper = await mountSuspended(RiskCard, { props: { goal: goal() } })

    await wrapper.get('[data-testid="risk-deliver"]').trigger('click')

    expect(wrapper.emitted('deliver')?.[0]).toEqual(['goal-1'])
  })

  it('offers no camera while a photograph is being looked at', async () => {
    const wrapper = await mountSuspended(RiskCard, {
      props: { goal: goal({ current: window({ pendingProofId: 'proof-1', acceptsProof: false }) }) },
    })

    expect(wrapper.find('[data-testid="risk-deliver"]').exists()).toBe(false)
  })

  it('is blocked from Session Replay, like every personal row', async () => {
    const wrapper = await mountSuspended(RiskCard, { props: { goal: goal() } })

    expect(wrapper.get('[data-testid="risk-card"]').attributes()).toHaveProperty('data-q2-block')
  })
})

function shared(id: string, title = 'Gemeinsames Ziel', status: SharedGoal['status'] = 'Active'): SharedGoal {
  return { id, title, icon: 'flame', status }
}

describe('BalanceCard', () => {
  it('shows the two numbers together, because neither means anything alone', async () => {
    const wrapper = await mountSuspended(BalanceCard, {
      props: { balance: { done: 47, missed: 5 } },
    })

    expect(wrapper.text()).toContain('47')
    expect(wrapper.get('[data-testid="balance-missed"]').text()).toBe('5')
    expect(wrapper.text()).toContain('geschafft')
    expect(wrapper.text()).toContain('verpasst')
  })

  it('says this week on your own profile, and only there (#27)', async () => {
    const week = { from: '2026-10-19', to: '2026-10-25', done: 3, missed: 1, relapsed: 0, open: 2 }
    const own = await mountSuspended(BalanceCard, {
      props: { balance: { done: 47, missed: 5, relapsed: 0 }, week },
    })

    expect(own.get('[data-testid="balance-week"]').text()).toContain('Diese Woche')
    expect(own.get('[data-testid="balance-week"]').text()).toContain('3 geschafft · 1 verpasst · 2 offen')

    const withRelapse = await mountSuspended(BalanceCard, {
      props: { balance: { done: 47, missed: 5, relapsed: 1 }, week: { ...week, relapsed: 1 } },
    })
    expect(withRelapse.get('[data-testid="balance-week"]').text()).toContain('1 Rückfall')

    const theirs = await mountSuspended(BalanceCard, {
      props: { balance: { done: 2, missed: 0, relapsed: 0 }, sharedGoals: [] },
    })
    expect(theirs.find('[data-testid="balance-week"]').exists()).toBe(false)
  })

  it('says so when there is nothing missed yet', async () => {
    const wrapper = await mountSuspended(BalanceCard, {
      props: { balance: { done: 12, missed: 0 } },
    })

    expect(wrapper.text()).toContain('Noch nichts verpasst.')
  })

  /**
   * "0 · 0" means two very different things. Without the count, a stranger's
   * profile would read as a spotless record rather than as an empty one.
   */
  it('tells nothing shared apart from a clean record', async () => {
    const nothing = await mountSuspended(BalanceCard, {
      props: { balance: { done: 0, missed: 0 }, sharedGoals: [] },
    })

    expect(nothing.text()).toContain('Ihr habt noch nichts gemeinsam.')
    expect(nothing.find('[data-testid="balance-missed"]').exists()).toBe(false)

    const clean = await mountSuspended(BalanceCard, {
      props: { balance: { done: 0, missed: 0 }, sharedGoals: [shared('goal-1'), shared('goal-2'), shared('goal-3')] },
    })

    expect(clean.get('[data-testid="balance-scope"]').text()).toBe('3 gemeinsame Dos & Don’ts')
    expect(clean.get('[data-testid="balance-missed"]').text()).toBe('0')
  })

  /**
   * A count with no way to what it counts left the reader guessing which goal
   * the record was about (#86). Each one is named and opens, and an ended one
   * stays — it is still in the numbers — saying that it ended.
   */
  it('names each goal it is counted over and opens it', async () => {
    const wrapper = await mountSuspended(BalanceCard, {
      props: {
        balance: { done: 4, missed: 1 },
        sharedGoals: [shared('goal-1', 'Jeden Morgen laufen'), shared('goal-2', 'Kalt duschen', 'Completed')],
      },
    })

    const rows = wrapper.findAll('[data-testid="balance-goal"]')
    expect(rows.map(row => row.attributes('href'))).toEqual(['/goals/goal-1', '/goals/goal-2'])
    expect(rows[0]!.text()).toBe('Jeden Morgen laufen')
    expect(rows[1]!.text()).toContain('Kalt duschen')
    expect(rows[1]!.text()).toContain('Abgeschlossen')
    expect(wrapper.get('ul').attributes('aria-labelledby')).toBe('balance-scope')
  })

  it('says nothing about scope on your own profile', async () => {
    const wrapper = await mountSuspended(BalanceCard, {
      props: { balance: { done: 47, missed: 5 } },
    })

    expect(wrapper.find('[data-testid="balance-scope"]').exists()).toBe(false)
  })

  it('is private, because a record is somebody\'s failures', async () => {
    const wrapper = await mountSuspended(BalanceCard, {
      props: { balance: { done: 47, missed: 5 } },
    })

    expect(wrapper.get('[data-testid="balance-card"]').attributes()).toHaveProperty('data-q2-private')
  })
})

describe('the feed', () => {
  /**
   * "Kein Nachtreten". The warning that somebody is about to miss used to be a
   * line in this feed, with the kudos button taken off it. It is a
   * notification to that person's friends now
   * (docs/adr/0024-one-notification-pipeline.md), so everything left here is
   * good news — and every line of good news can be cheered.
   */
  it('leaves the button on everything that is good news', async () => {
    const ordinary = await mountSuspended(ActivityRow, {
      props: { activity: activity(), now: Date.now() },
    })

    expect(ordinary.get('[data-testid="kudos-button"]').exists()).toBe(true)
  })
})
