import { mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, describe, expect, it } from 'vitest'
import GoalChangeBanner from '~/components/goals/GoalChangeBanner.vue'
import GoalEditSheet from '~/components/goals/GoalEditSheet.vue'
import type { Friend, Goal, GoalChange, GoalEditPreview, Person } from '~/api/types'

/**
 * Editing a running goal, on screen (docs/adr/0044-editing-a-running-goal.md):
 * the sheet starts from the goal as it will be, asks the server what saving
 * would do before it saves, and says so — and the banner puts a waiting change
 * in front of the right person, with a count and never a name.
 */
function person(overrides: Partial<Person> = {}): Person {
  return {
    id: 'person-lena',
    displayName: 'Lena',
    handle: '@lena',
    initials: 'L',
    avatarColor: '#4f46e5',
    avatarImageId: null,
    isOnline: false,
    ...overrides,
  }
}

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
    iconFrom: null,
    iconTo: null,
    descriptionChanged: false,
    descriptionTo: null,
    scheduleFrom: { kind: 'Times', everyDays: null, weekdays: [], times: 3, period: 'Week' },
    scheduleTo: { kind: 'Times', everyDays: null, weekdays: [], times: 2, period: 'Week' },
    limitFrom: null,
    limitTo: null,
    proofModeFrom: null,
    proofModeTo: null,
    targetDateFrom: null,
    targetDateTo: null,
    checkersAdded: [],
    checkersRemoved: [],
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
    title: 'Laufen',
    description: null,
    icon: 'medal',
    schedule: { kind: 'Times', everyDays: null, weekdays: [], times: 3, period: 'Week' },
    status: 'Active',
    isGroup: false,
    kind: 'Do',
    current: null,
    streak: 4,
    windowsDone: 12,
    windowsMissed: 2,
    reminderAt: null,
    targetDate: null,
    createdAt: '2026-05-15T09:00:00+00:00',
    closedAt: null,
    participants: [person()],
    isOverdue: false,
    isMine: true,
    risk: null,
    pause: null,
    remainingPauses: 2,
    pendingChange: null,
    ...overrides,
  } as Goal
}

function friend(overrides: Partial<Person> = {}): Friend {
  return { person: person(overrides), streak: 0, lastSeenAt: null }
}

function element<T extends Element>(selector: string): T {
  const found = document.querySelector<T>(selector)
  expect(found, `Expected ${selector} to be rendered by the open drawer`).not.toBeNull()
  return found!
}

function type(input: HTMLInputElement, value: string) {
  input.value = value
  input.dispatchEvent(new Event('input', { bubbles: true }))
}

afterEach(() => {
  document.body.replaceChildren()
})

describe('GoalEditSheet', () => {
  async function next(wrapper: { vm: { $nextTick: () => Promise<void> } }) {
    element<HTMLButtonElement>('[data-testid="goal-edit-next"]').click()
    await wrapper.vm.$nextTick()
  }

  it('starts from the goal, asks for a preview on the last step and sends everything', async () => {
    const wrapper = await mountSuspended(GoalEditSheet, {
      props: { open: true, goal: goal(), friends: [friend(), friend({ id: 'person-jonas', displayName: 'Jonas' })] },
      attachTo: document.body,
    })

    expect(element<HTMLInputElement>('[data-testid="goal-edit-title"] input, input[data-testid="goal-edit-title"]').value).toBe('Laufen')

    type(element<HTMLInputElement>('[data-testid="goal-edit-title"] input, input[data-testid="goal-edit-title"]'), 'Joggen ')
    await wrapper.vm.$nextTick()
    await next(wrapper)

    // A repeating goal keeps repeating: the one-off is not on offer.
    expect(document.querySelector('[data-testid="schedule-kind-Once"]')).toBeNull()
    element<HTMLButtonElement>('[data-testid="schedule-times-2"]').click()
    await wrapper.vm.$nextTick()
    await next(wrapper)

    element<HTMLElement>('[data-testid="goal-friend-person-jonas"] [role="checkbox"]').click()
    await wrapper.vm.$nextTick()
    await next(wrapper)

    const [request] = wrapper.emitted('preview')![0] as [Record<string, unknown>]
    expect(request).toMatchObject({
      title: 'Joggen',
      icon: 'medal',
      schedule: { kind: 'Times', times: 2, period: 'Week' },
      participantIds: ['person-lena', 'person-jonas'],
    })
    expect(request).not.toHaveProperty('limit')

    // Nothing to save before the server has said what saving does.
    expect(element<HTMLButtonElement>('[data-testid="goal-edit-submit"]').disabled).toBe(true)
  })

  it('says before saving that the checkers can object, and until when', async () => {
    const preview: GoalEditPreview = {
      change: change({ titleFrom: 'Laufen', titleTo: 'Joggen' }),
      withdrawsPending: false,
    }

    const wrapper = await mountSuspended(GoalEditSheet, {
      props: { open: true, goal: goal(), friends: [], preview },
      attachTo: document.body,
    })

    for (let index = 0; index < 3; index++) await next(wrapper)

    expect(element('[data-testid="goal-edit-now"]').textContent).toContain('Titel: „Laufen“ → „Joggen“')
    expect(element('[data-testid="goal-edit-later"]').textContent).toContain('Gilt ab Mi, 17.6.')
    expect(element('[data-testid="goal-edit-later"]').textContent).toContain('Rhythmus: 3× pro Woche → 2× pro Woche')
    expect(element('[data-testid="goal-edit-notice"]').textContent).toContain('Deine Prüfer können bis')

    element<HTMLButtonElement>('[data-testid="goal-edit-submit"]').click()
    await wrapper.vm.$nextTick()
    expect(wrapper.emitted('submit')).toHaveLength(1)
  })

  it('says a stricter change applies without objection', async () => {
    const preview: GoalEditPreview = {
      change: change({
        objectable: false,
        objectionEndsAt: null,
        scheduleTo: { kind: 'Times', everyDays: null, weekdays: [], times: 4, period: 'Week' },
      }),
      withdrawsPending: false,
    }

    const wrapper = await mountSuspended(GoalEditSheet, {
      props: { open: true, goal: goal(), friends: [], preview },
      attachTo: document.body,
    })

    for (let index = 0; index < 3; index++) await next(wrapper)

    expect(element('[data-testid="goal-edit-notice"]').textContent).toContain('ohne Einspruch')
  })

  it('has nothing to save when nothing changed', async () => {
    const wrapper = await mountSuspended(GoalEditSheet, {
      props: { open: true, goal: goal(), friends: [], preview: { change: null, withdrawsPending: false } },
      attachTo: document.body,
    })

    for (let index = 0; index < 3; index++) await next(wrapper)

    expect(element('[data-testid="goal-edit-nothing"]').textContent).toContain('nichts geändert')
    expect(element<HTMLButtonElement>('[data-testid="goal-edit-submit"]').disabled).toBe(true)
  })

  /**
   * An edit of the title must not take back yesterday's schedule change: the
   * sheet opens on the goal as the waiting change will leave it.
   */
  it('opens on the waiting change, not on the goal as it is now', async () => {
    const wrapper = await mountSuspended(GoalEditSheet, {
      props: {
        open: true,
        goal: goal({ pendingChange: change({ checkersRemoved: [person()] }), participants: [person(), person({ id: 'person-jonas', displayName: 'Jonas' })] }),
        friends: [],
      },
      attachTo: document.body,
    })

    for (let index = 0; index < 3; index++) await next(wrapper)

    const [request] = wrapper.emitted('preview')![0] as [{ schedule: { times: number }, participantIds: string[] }]
    expect(request.schedule.times).toBe(2)
    expect(request.participantIds).toEqual(['person-jonas'])
  })

  it('offers a Don\'t its limit, never a rhythm', async () => {
    const wrapper = await mountSuspended(GoalEditSheet, {
      props: {
        open: true,
        goal: goal({
          kind: 'Dont',
          schedule: { kind: 'Limit', everyDays: null, weekdays: [], times: null, period: 'Week' },
          limit: 1,
          proofMode: 'HonestOnly',
        }),
        friends: [],
      },
      attachTo: document.body,
    })

    await next(wrapper)

    expect(document.querySelector('[data-testid="schedule-picker"]')).toBeNull()
    element<HTMLButtonElement>('[data-testid="dont-limit-less"]').click()
    await wrapper.vm.$nextTick()
    await next(wrapper)
    await next(wrapper)

    const [request] = wrapper.emitted('preview')![0] as [Record<string, unknown>]
    expect(request).toMatchObject({ schedule: { kind: 'Limit', period: 'Week' }, limit: 0, proofMode: 'HonestOnly' })
  })
})

describe('GoalChangeBanner', () => {
  it('gives the owner the way back and no way to object to themselves', async () => {
    const wrapper = await mountSuspended(GoalChangeBanner, {
      props: { change: change(), isMine: true },
    })

    expect(wrapper.get('[data-testid="change-banner-title"]').text()).toContain('ab Mi, 17.6.')
    expect(wrapper.text()).toContain('Rhythmus: 3× pro Woche → 2× pro Woche')
    expect(wrapper.find('[data-testid="change-veto"]').exists()).toBe(false)

    await wrapper.get('[data-testid="change-withdraw"]').trigger('click')
    expect(wrapper.emitted('withdraw')).toHaveLength(1)
  })

  it('shows a checker a count and never a name', async () => {
    const wrapper = await mountSuspended(GoalChangeBanner, {
      props: { change: change(), isMine: false },
    })

    expect(wrapper.find('[data-testid="change-withdraw"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('1 von 2 Einsprüchen')
    expect(wrapper.text()).toContain('Einspruch möglich bis')

    await wrapper.get('[data-testid="change-veto"]').trigger('click')
    expect(wrapper.emitted('veto')).toHaveLength(1)
  })

  it('offers no objection to a stricter change', async () => {
    const wrapper = await mountSuspended(GoalChangeBanner, {
      props: { change: change({ objectable: false, objectionEndsAt: null, canVeto: false }), isMine: false },
    })

    expect(wrapper.find('[data-testid="change-veto"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="change-banner-until"]').exists()).toBe(false)
  })

  it('keeps what changed out of a session replay', async () => {
    const wrapper = await mountSuspended(GoalChangeBanner, {
      props: { change: change(), isMine: true },
    })

    expect(wrapper.get('[data-testid="change-banner"]').attributes()).toHaveProperty('data-q2-block')
  })
})
