import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import ActivityRow from '~/components/social/ActivityRow.vue'
import BalanceCard from '~/components/profile/BalanceCard.vue'
import RiskCard from '~/components/goals/RiskCard.vue'
import type { Activity, Goal, GoalWindow } from '~/api/types'

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
    icon: 'medal',
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
    subject: 'Joggen 5 km',
    amount: null,
    kudosCount: 3,
    hasMyKudos: false,
    occurredAt: '2026-06-15T08:00:00+00:00',
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
      props: { balance: { done: 0, missed: 0 }, sharedGoals: 0 },
    })

    expect(nothing.text()).toContain('Ihr habt noch nichts gemeinsam.')
    expect(nothing.find('[data-testid="balance-missed"]').exists()).toBe(false)

    const clean = await mountSuspended(BalanceCard, {
      props: { balance: { done: 0, missed: 0 }, sharedGoals: 3 },
    })

    expect(clean.get('[data-testid="balance-scope"]').text()).toBe('3 gemeinsame To-Dos')
    expect(clean.get('[data-testid="balance-missed"]').text()).toBe('0')
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

describe('a warning in the feed', () => {
  /**
   * "Kein Nachtreten". All three kudos are approving, so the button could never
   * be an insult on its own — but "stark gemacht" under "droht zu verpassen" is
   * a sentence nobody should be able to send.
   */
  it('offers no kudos button', async () => {
    const warning = await mountSuspended(ActivityRow, {
      props: { activity: activity({ kind: 'WindowAtRisk', subject: 'Jeden Tag laufen' }), now: Date.now() },
    })

    expect(warning.find('[data-testid="kudos-button"]').exists()).toBe(false)
    expect(warning.get('[data-testid="activity-warning"]').exists()).toBe(true)
    expect(warning.text()).toContain('droht „Jeden Tag laufen“ zu verpassen')
  })

  it('leaves the button on everything that is good news', async () => {
    const ordinary = await mountSuspended(ActivityRow, {
      props: { activity: activity(), now: Date.now() },
    })

    expect(ordinary.get('[data-testid="kudos-button"]').exists()).toBe(true)
  })
})
