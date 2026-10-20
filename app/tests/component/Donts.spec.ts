import { mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, describe, expect, it } from 'vitest'
import BalanceCard from '~/components/profile/BalanceCard.vue'
import DontHero from '~/components/donts/DontHero.vue'
import DontLimitPicker from '~/components/donts/DontLimitPicker.vue'
import DontWindowRow from '~/components/donts/DontWindowRow.vue'
import GoalCard from '~/components/goals/GoalCard.vue'
import GoalCreateSheet from '~/components/goals/GoalCreateSheet.vue'
import SpotCheckCard from '~/components/donts/SpotCheckCard.vue'
import type { Friend, Goal, GoalWindow } from '~/api/types'

/**
 * A Don't on screen (docs/adr/0037-donts.md): never struck through as "done"
 * while its period runs, a run where a Do has a streak, one tap to log a use —
 * and a question first when that tap would be the relapse.
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
    category: 'Other',
    icon: 'target',
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
    spotCheck: null,
    ...overrides,
  }
}

function element<T extends Element>(selector: string): T {
  const found = document.querySelector<T>(selector)
  expect(found, `Expected ${selector} to be rendered`).not.toBeNull()
  return found!
}

afterEach(() => {
  document.body.replaceChildren()
})

describe('DontWindowRow', () => {
  it('shows the limit and what is used, and is never struck through as done', async () => {
    const wrapper = await mountSuspended(DontWindowRow, { props: { goal: dont() } })

    expect(wrapper.text()).toContain('Höchstens 3× pro Woche')
    expect(wrapper.get('[data-testid="dont-used"]').text()).toBe('1 von 3 genutzt')
    expect(wrapper.html()).not.toContain('line-through')
    expect(wrapper.find('[data-testid="window-deliver"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="dont-row"]').attributes()).toHaveProperty('data-q2-block')
  })

  it('logs a use in one tap while the limit has room', async () => {
    const wrapper = await mountSuspended(DontWindowRow, { props: { goal: dont() } })

    const log = wrapper.get('[data-testid="dont-log"]')
    expect(log.attributes('aria-label')).toBe('Nutzung eintragen: Höchstens 3 Bier')

    await log.trigger('click')
    expect(wrapper.emitted('log')?.[0]).toEqual(['dont-1'])
  })

  it('asks first when the tap would be the relapse', async () => {
    const wrapper = await mountSuspended(DontWindowRow, {
      props: { goal: dont({ limit: 0, current: window({ limit: 0, uses: 0 }) }) },
      attachTo: document.body,
    })

    await wrapper.get('[data-testid="dont-log"]').trigger('click')
    expect(wrapper.emitted('log')).toBeUndefined()

    await wrapper.vm.$nextTick()
    expect(element('[data-testid="confirm-dialog"]')).toBeTruthy()
    expect(document.body.textContent).toContain('Als Rückfall eintragen?')
    wrapper.unmount()
  })

  it('says a relapse, and offers nothing to whoever does not own it', async () => {
    const wrapper = await mountSuspended(DontWindowRow, {
      props: { goal: dont({ isMine: false, current: window({ status: 'Relapse', uses: 4 }) }) },
    })

    expect(wrapper.get('[data-testid="dont-used"]').text()).toBe('Rückfall')
    expect(wrapper.get('[data-testid="dont-state"]').attributes('aria-label')).toBe('Rückfall')
    expect(wrapper.find('[data-testid="dont-log"]').exists()).toBe(false)
  })

  it('offers the camera while a proof at the end is still owed', async () => {
    const wrapper = await mountSuspended(DontWindowRow, {
      props: { goal: dont({ proofMode: 'ProofAtEnd', current: window({ requiredProofs: 1, remainingProofs: 1, acceptsProof: true }) }) },
    })

    await wrapper.get('[data-testid="window-deliver"]').trigger('click')
    expect(wrapper.emitted('deliver')?.[0]).toEqual(['dont-1'])
  })
})

describe('DontHero', () => {
  it('counts the run, says the period and how friends check it', async () => {
    const wrapper = await mountSuspended(DontHero, { props: { goal: dont(), today: '2026-06-22' } })

    expect(wrapper.get('[data-testid="dont-run"]').text()).toBe('Seit 12 Tagen')
    expect(wrapper.get('[data-testid="dont-period"]').text()).toBe('1 von 3 genutzt')
    expect(wrapper.get('[data-testid="dont-proof-mode"]').text()).toContain('Nur ehrlich')
  })

  it('lets the owner log, with or without a photograph', async () => {
    const wrapper = await mountSuspended(DontHero, { props: { goal: dont(), today: '2026-06-22' } })

    await wrapper.get('[data-testid="dont-log"]').trigger('click')
    await wrapper.get('[data-testid="dont-log-photo"]').trigger('click')

    expect(wrapper.emitted('log')).toHaveLength(1)
    expect(wrapper.emitted('logWithPhoto')).toHaveLength(1)
  })

  it('offers a friend nothing to press', async () => {
    const wrapper = await mountSuspended(DontHero, { props: { goal: dont({ isMine: false }), today: '2026-06-22' } })

    expect(wrapper.find('[data-testid="dont-log"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="dont-log-photo"]').exists()).toBe(false)
  })
})

describe('DontLimitPicker', () => {
  it('starts at never, steps up and explains what the limit means', async () => {
    const wrapper = await mountSuspended(DontLimitPicker, {
      props: { modelValue: { limit: 0, period: 'Day', proofMode: 'HonestOnly' } },
    })

    expect(wrapper.get('[data-testid="dont-limit"]').text()).toBe('Nie')
    expect(wrapper.get('[data-testid="dont-limit-less"]').attributes()).toHaveProperty('disabled')
    expect(wrapper.get('[data-testid="dont-limit-explanation"]').text()).toContain('Kein einziges Mal')

    await wrapper.get('[data-testid="dont-limit-more"]').trigger('click')
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([{ limit: 1, period: 'Day', proofMode: 'HonestOnly' }])

    await wrapper.get('[data-testid="dont-mode-ProofAtEnd"]').trigger('click')
    expect(wrapper.emitted('update:modelValue')?.[1]).toEqual([{ limit: 1, period: 'Day', proofMode: 'ProofAtEnd' }])
  })
})

describe('GoalCard, for a Don\'t', () => {
  it('shows its limit and its run where a Do shows its rhythm and its streak', async () => {
    const wrapper = await mountSuspended(GoalCard, { props: { goal: dont(), today: '2026-06-22' } })

    expect(wrapper.text()).toContain('Höchstens 3× pro Woche')
    expect(wrapper.get('[data-testid="goal-run"]').text()).toBe('Seit 12 Tagen')
    expect(wrapper.get('[data-testid="goal-window"]').text()).toBe('1 von 3 genutzt')
  })
})

describe('BalanceCard', () => {
  it('counts relapses on their own, and only when there are any', async () => {
    const withRelapse = await mountSuspended(BalanceCard, { props: { balance: { done: 5, missed: 1, relapsed: 2 } } })
    expect(withRelapse.get('[data-testid="balance-relapsed"]').text()).toBe('2 Rückfälle')
    expect(withRelapse.get('[data-testid="balance-missed"]').text()).toBe('1')

    const without = await mountSuspended(BalanceCard, { props: { balance: { done: 5, missed: 1, relapsed: 0 } } })
    expect(without.find('[data-testid="balance-relapsed"]').exists()).toBe(false)
  })
})

describe('GoalCreateSheet, for a Don\'t', () => {
  function friends(): Friend[] {
    return [{
      person: { id: 'person-1', displayName: 'Mara Beispiel', handle: '@mara', initials: 'MB', avatarColor: '#4f46e5', avatarImageId: null, isOnline: true },
      streak: 0,
      lastSeenAt: null,
    }]
  }

  async function next(wrapper: { vm: { $nextTick: () => Promise<void> } }) {
    element<HTMLButtonElement>('[data-testid="goal-next"]').click()
    await wrapper.vm.$nextTick()
  }

  it('makes a Don\'t from an idea, with a limit, a period and a way to check it', async () => {
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: friends() },
      attachTo: document.body,
    })

    element<HTMLElement>('[data-testid="segment-Dont"]').click()
    await wrapper.vm.$nextTick()

    element<HTMLButtonElement>('[data-testid="dont-template-fastFood"]').click()
    await wrapper.vm.$nextTick()
    await next(wrapper)

    expect(element('[data-testid="goal-create-step"]').textContent).toContain('Limit')
    expect(element('[data-testid="dont-limit"]').textContent).toBe('2×')

    element<HTMLButtonElement>('[data-testid="dont-limit-more"]').click()
    await wrapper.vm.$nextTick()
    element<HTMLButtonElement>('[data-testid="dont-mode-ProofAtEnd"]').click()
    await wrapper.vm.$nextTick()
    await next(wrapper)

    element<HTMLElement>('[data-testid="goal-friend-person-1"] [role="checkbox"]').click()
    await wrapper.vm.$nextTick()
    await next(wrapper)

    const review = element('[data-testid="goal-create-review"]')
    expect(review.textContent).toContain('Höchstens 3× pro Woche')
    expect(review.textContent).toContain('Beweis am Ende')

    element<HTMLButtonElement>('[data-testid="goal-submit"]').click()
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('submit')?.[0]).toEqual([{
      title: 'Höchstens 2× Fast Food pro Woche',
      kind: 'Dont',
      schedule: { kind: 'Limit', period: 'Week' },
      limit: 3,
      proofMode: 'ProofAtEnd',
      category: 'Other',
      reminderAt: null,
      participantIds: ['person-1'],
    }])
    wrapper.unmount()
  })
})

describe('SpotCheckCard', () => {
  // Relative to the real clock: once mounted the card keeps its own.
  const now = Date.now()
  const inSeconds = (seconds: number) => new Date(now + seconds * 1000).toISOString()

  it('counts the three minutes down and opens the camera for this check', async () => {
    const wrapper = await mountSuspended(SpotCheckCard, {
      props: {
        goal: dont({ spotCheck: { id: 'check-1', expiresAt: inSeconds(161) } }),
        now,
      },
    })

    // The first frame is the shared clock's, as on the server.
    expect(wrapper.get('[data-testid="spot-check-left"]').text()).toMatch(/^noch \d:\d{2}$/)
    expect(wrapper.text()).toContain('Höchstens 3 Bier')

    const answer = wrapper.get('[data-testid="spot-check-answer"]')
    expect(answer.attributes('aria-label')).toBe('Stichprobe beantworten: Höchstens 3 Bier')

    await answer.trigger('click')
    expect(wrapper.emitted('answer')?.[0]).toEqual(['dont-1', 'check-1'])
  })

  it('says when the time is up and offers nothing more', async () => {
    const wrapper = await mountSuspended(SpotCheckCard, {
      props: {
        goal: dont({ spotCheck: { id: 'check-1', expiresAt: inSeconds(-60) } }),
        now,
      },
    })

    await wrapper.vm.$nextTick()
    expect(wrapper.get('[data-testid="spot-check-left"]').text()).toBe('Die Zeit ist um.')
    expect(wrapper.get('[data-testid="spot-check-answer"]').attributes()).toHaveProperty('disabled')
  })

  it('is not drawn without an open check', async () => {
    const wrapper = await mountSuspended(SpotCheckCard, { props: { goal: dont({ spotCheck: null }), now } })

    expect(wrapper.find('[data-testid="spot-check"]').exists()).toBe(false)
  })
})
