import { mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, describe, expect, it } from 'vitest'
import GoalCard from '~/components/goals/GoalCard.vue'
import GoalCloseSheet from '~/components/goals/GoalCloseSheet.vue'
import GoalPauseBanner from '~/components/goals/GoalPauseBanner.vue'
import GoalPauseSheet from '~/components/goals/GoalPauseSheet.vue'
import ChatGoalBanner from '~/components/chats/ChatGoalBanner.vue'
import type { ChatPinnedGoal, Goal, GoalPause } from '~/api/types'

/**
 * The exits, on screen.
 *
 * Most of these assertions are about what is *not* there: an objection with a
 * name on it, a red banner over somebody's illness, an end button offered to
 * the wrong person. The rule is decided on the server either way — this is
 * about not putting the question in front of the wrong person at all.
 */
function pause(overrides: Partial<GoalPause> = {}): GoalPause {
  return {
    id: 'pause-1',
    reason: 'Grippe, seit Freitag im Bett.',
    startsOn: '2026-06-15',
    endsOn: '2026-06-18',
    endsAt: '2026-06-18T21:59:59+00:00',
    days: 4,
    vetoCount: 1,
    vetoesRequired: 2,
    vetoedByMe: false,
    canVeto: true,
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
    current: null,
    streak: 4,
    windowsDone: 12,
    windowsMissed: 2,
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

function element<T extends Element>(selector: string): T {
  const found = document.querySelector<T>(selector)
  expect(found, `Expected ${selector} to be rendered by the open drawer`).not.toBeNull()
  return found!
}

function type(field: HTMLTextAreaElement, value: string) {
  field.value = value
  field.dispatchEvent(new Event('input', { bubbles: true }))
}

afterEach(() => {
  document.body.replaceChildren()
})

describe('GoalPauseSheet', () => {
  it('will not submit an excuse that is not a sentence', async () => {
    const wrapper = await mountSuspended(GoalPauseSheet, {
      props: { open: true, remaining: 2, maxDays: 7, minReason: 10, submitting: false },
      attachTo: document.body,
    })

    const submit = element<HTMLButtonElement>('[data-testid="pause-submit"]')
    expect(submit.disabled).toBe(true)

    type(element<HTMLTextAreaElement>('[data-testid="pause-reason"]'), 'krank')
    await wrapper.vm.$nextTick()
    expect(submit.disabled).toBe(true)

    type(element<HTMLTextAreaElement>('[data-testid="pause-reason"]'), '  Grippe, seit Freitag im Bett.  ')
    await wrapper.vm.$nextTick()
    expect(submit.disabled).toBe(false)

    element<HTMLButtonElement>('[data-testid="pause-days-3"]').click()
    await wrapper.vm.$nextTick()
    submit.click()

    expect(wrapper.emitted('submit')?.[0]).toEqual([
      { reason: 'Grippe, seit Freitag im Bett.', days: 3 },
    ])
  })

  /**
   * The scarcity is the brake, so it is on the screen rather than behind it —
   * and when it is gone, the sheet says so instead of letting somebody write an
   * excuse that will be refused.
   */
  it('shows the allowance and closes the door when it is used up', async () => {
    await mountSuspended(GoalPauseSheet, {
      props: { open: true, remaining: 1, maxDays: 7, minReason: 10, submitting: false },
      attachTo: document.body,
    })

    expect(element('[data-testid="pause-allowance"]').textContent).toContain('Noch 1 Pause diesen Monat')

    document.body.replaceChildren()

    await mountSuspended(GoalPauseSheet, {
      props: { open: true, remaining: 0, maxDays: 7, minReason: 10, submitting: false },
      attachTo: document.body,
    })

    expect(element('[data-testid="pause-allowance"]').textContent)
      .toContain('Diesen Monat ist keine Pause mehr übrig')
    expect(element<HTMLTextAreaElement>('[data-testid="pause-reason"]').disabled).toBe(true)
    expect(element<HTMLButtonElement>('[data-testid="pause-submit"]').disabled).toBe(true)
  })

  it('offers exactly the days the server would accept', async () => {
    await mountSuspended(GoalPauseSheet, {
      props: { open: true, remaining: 2, maxDays: 7, minReason: 10, submitting: false },
      attachTo: document.body,
    })

    expect(document.querySelectorAll('[data-testid^="pause-days-"]')).toHaveLength(7)
    expect(document.querySelector('[data-testid="pause-days-8"]')).toBeNull()
  })
})

describe('GoalPauseBanner', () => {
  it('gives the owner the way back and no way to object to themselves', async () => {
    const wrapper = await mountSuspended(GoalPauseBanner, {
      props: { pause: pause(), isMine: true },
    })

    expect(wrapper.get('[data-testid="pause-reason-text"]').text()).toBe('Grippe, seit Freitag im Bett.')
    expect(wrapper.text()).toContain('Bis 18.6.')

    expect(wrapper.find('[data-testid="pause-veto"]').exists()).toBe(false)

    await wrapper.get('[data-testid="pause-end"]').trigger('click')
    expect(wrapper.emitted('end')).toHaveLength(1)
  })

  /**
   * An objection is a count, never a list. The component cannot show a name
   * because the response does not carry one.
   */
  it('shows an invited friend a count and never a name', async () => {
    const wrapper = await mountSuspended(GoalPauseBanner, {
      props: { pause: pause(), isMine: false },
    })

    expect(wrapper.find('[data-testid="pause-end"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('1 von 2 Einsprüchen')
    expect(wrapper.text()).toContain('Einsprüche sind anonym.')

    await wrapper.get('[data-testid="pause-veto"]').trigger('click')
    expect(wrapper.emitted('veto')).toHaveLength(1)
  })

  it('offers to take an objection back once it has been made', async () => {
    const wrapper = await mountSuspended(GoalPauseBanner, {
      props: { pause: pause({ vetoedByMe: true }), isMine: false },
    })

    expect(wrapper.get('[data-testid="pause-veto"]').text()).toContain('zurückziehen')
  })

  it('says nothing to somebody who was not invited', async () => {
    const wrapper = await mountSuspended(GoalPauseBanner, {
      props: { pause: pause({ canVeto: false }), isMine: false },
    })

    expect(wrapper.find('[data-testid="pause-veto"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="pause-end"]').exists()).toBe(false)
  })
})

describe('GoalCloseSheet', () => {
  it('offers both endings and says which was chosen', async () => {
    const wrapper = await mountSuspended(GoalCloseSheet, {
      props: { open: true, submitting: false },
      attachTo: document.body,
    })

    element<HTMLButtonElement>('[data-testid="close-completed"]').click()
    expect(wrapper.emitted('close')?.[0]).toEqual([true])

    element<HTMLButtonElement>('[data-testid="close-archived"]').click()
    expect(wrapper.emitted('close')?.[1]).toEqual([false])
  })

  /** The whole reason the exit exists: it does not cost anybody their record. */
  it('promises the record survives', async () => {
    await mountSuspended(GoalCloseSheet, {
      props: { open: true, submitting: false },
      attachTo: document.body,
    })

    expect(document.body.textContent).toContain('weder als geschafft noch als verpasst')
  })
})

describe('a paused goal in a list', () => {
  it('is marked as resting rather than as finished', async () => {
    const wrapper = await mountSuspended(GoalCard, {
      props: { goal: goal({ pause: pause() }) },
    })

    expect(wrapper.get('[data-testid="goal-paused"]').text()).toBe('Ausgesetzt')
    expect(wrapper.get('[data-testid="goal-window"]').text()).toBe('Bis 18.6.')
  })

  it('falls back to its status when it has stopped for good', async () => {
    const wrapper = await mountSuspended(GoalCard, {
      props: { goal: goal({ status: 'Completed', closedAt: '2026-06-14T09:00:00+00:00' }) },
    })

    expect(wrapper.find('[data-testid="goal-paused"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="goal-window"]').text()).toBe('Abgeschlossen')
  })
})

describe('ChatGoalBanner', () => {
  it('says a pinned goal is set aside, and leaves the objection to its own screen', async () => {
    const pinned = {
      id: 'goal-1',
      title: 'Halbmarathon',
      current: null,
      streak: 2,
      pausedUntil: '2026-06-18',
    } as ChatPinnedGoal

    const wrapper = await mountSuspended(ChatGoalBanner, { props: { goal: pinned } })

    expect(wrapper.get('[data-testid="chat-goal-paused"]').text()).toContain('Ausgesetzt')
    expect(wrapper.get('[data-testid="chat-goal-paused"]').text()).toContain('Bis 18.6.')

    // No reason and no objection here — a chat is not where somebody is asked
    // to judge a friend's illness.
    expect(wrapper.text()).not.toContain('Grippe')
    expect(wrapper.find('[data-testid="pause-veto"]').exists()).toBe(false)
  })
})
