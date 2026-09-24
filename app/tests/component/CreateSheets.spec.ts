import { mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, describe, expect, it } from 'vitest'
import ChatCreateSheet from '~/components/chats/ChatCreateSheet.vue'
import GoalCreateSheet from '~/components/goals/GoalCreateSheet.vue'
import type { ApiFailure } from '~/api/errors'
import type { Friend, Person } from '~/api/types'

function person(overrides: Partial<Person> = {}): Person {
  return {
    id: 'person-1',
    displayName: 'Mara Beispiel',
    handle: '@mara',
    initials: 'MB',
    avatarColor: '#4f46e5',
    avatarImageId: null,
    isOnline: true,
    ...overrides,
  }
}

function friend(): Friend {
  return { person: person(), streak: 3, lastSeenAt: null }
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

describe('GoalCreateSheet', () => {
  function friends(count: number): Friend[] {
    return Array.from({ length: count }, (_, index) => ({
      person: person({ id: `person-${index + 1}`, displayName: index === 0 ? 'Mara Beispiel' : `Freund ${index + 1}`, handle: `@f${index + 1}` }),
      streak: 0,
      lastSeenAt: null,
    }))
  }

  async function next(wrapper: { vm: { $nextTick: () => Promise<void> } }) {
    element<HTMLButtonElement>('[data-testid="goal-next"]').click()
    await wrapper.vm.$nextTick()
  }

  it('walks through four steps and emits the complete trimmed request', async () => {
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: [friend()] },
      attachTo: document.body,
    })

    expect(element('[data-testid="goal-create-step"]').textContent).toContain('Schritt 1 von 4')

    // Nothing to go on with before there is a title.
    expect(element<HTMLButtonElement>('[data-testid="goal-next"]').disabled).toBe(true)

    type(element<HTMLInputElement>('[data-testid="goal-title-input"]'), '  Neues Ziel  ')
    element<HTMLButtonElement>('[data-testid="icon-flame"]').click()
    await wrapper.vm.$nextTick()
    await next(wrapper)

    element<HTMLButtonElement>('[data-testid="schedule-kind-Times"]').click()
    await wrapper.vm.$nextTick()
    element<HTMLButtonElement>('[data-testid="schedule-times-3"]').click()
    await wrapper.vm.$nextTick()

    // The preview says what the choice means, in the words the goal will use.
    expect(element('[data-testid="schedule-preview"]').textContent).toContain('3× pro Woche')

    type(element<HTMLInputElement>('[data-testid="goal-reminder-input"]'), '07:15')
    await wrapper.vm.$nextTick()
    await next(wrapper)

    // A title is not enough: somebody has to check it.
    expect(element<HTMLButtonElement>('[data-testid="goal-next"]').disabled).toBe(true)
    element<HTMLElement>('[data-testid="goal-friend-person-1"] [role="checkbox"]').click()
    await wrapper.vm.$nextTick()
    await next(wrapper)

    // The summary says what is about to happen before anything is committed.
    const review = element('[data-testid="goal-create-review"]')
    expect(review.textContent).toContain('Neues Ziel')
    expect(review.textContent).toContain('3× pro Woche')
    expect(review.textContent).toContain('Mara Beispiel')
    expect(review.textContent).toContain('07:15')
    expect(element('[data-testid="goal-first-window"]').textContent).toMatch(/Dein erstes Fenster endet/)

    element<HTMLButtonElement>('[data-testid="goal-submit"]').click()
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('submit')?.[0]).toEqual([{
      title: 'Neues Ziel',
      schedule: { kind: 'Times', times: 3, period: 'Week' },
      icon: 'flame',
      reminderAt: '07:15:00',
      participantIds: ['person-1'],
    }])
    wrapper.unmount()
  })

  it('goes back a step without losing what was entered', async () => {
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: [friend()] },
      attachTo: document.body,
    })

    type(element<HTMLInputElement>('[data-testid="goal-title-input"]'), 'Laufen')
    await wrapper.vm.$nextTick()
    await next(wrapper)

    element<HTMLButtonElement>('[data-testid="goal-back"]').click()
    await wrapper.vm.$nextTick()

    expect(element<HTMLInputElement>('[data-testid="goal-title-input"]').value).toBe('Laufen')
    wrapper.unmount()
  })

  it('fills the title, the icon and the rhythm from an idea', async () => {
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: [friend()] },
      attachTo: document.body,
    })

    element<HTMLButtonElement>('[data-testid="goal-template-tidy"]').click()
    await wrapper.vm.$nextTick()

    expect(element<HTMLInputElement>('[data-testid="goal-title-input"]').value).toBe('Jeden Sonntag aufräumen')
    expect(element('[data-testid="icon-sparkles"]').getAttribute('aria-checked')).toBe('true')

    // Once there is a title, the ideas get out of the way.
    expect(document.querySelector('[data-testid="goal-template-run"]')).toBeNull()

    await next(wrapper)
    expect(element('[data-testid="schedule-weekday-Sunday"]').getAttribute('aria-checked')).toBe('true')
    wrapper.unmount()
  })

  it('keeps the names of the friends to choose from out of Session Replay', async () => {
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: [friend()] },
      attachTo: document.body,
    })

    type(element<HTMLInputElement>('[data-testid="goal-title-input"]'), 'Laufen')
    await wrapper.vm.$nextTick()
    await next(wrapper)
    await next(wrapper)

    const row = element('[data-testid="goal-friend-person-1"]')
    expect(row.textContent).toContain('Mara Beispiel')
    expect(row.querySelector('[data-q2-private]')).not.toBeNull()

    // One friend is not a list worth searching.
    expect(document.querySelector('[data-testid="goal-friend-search"]')).toBeNull()
    wrapper.unmount()
  })

  it('searches a long list of friends by name or handle', async () => {
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: friends(8) },
      attachTo: document.body,
    })

    type(element<HTMLInputElement>('[data-testid="goal-title-input"]'), 'Laufen')
    await wrapper.vm.$nextTick()
    await next(wrapper)
    await next(wrapper)

    type(element<HTMLInputElement>('[data-testid="goal-friend-search"] input, input[data-testid="goal-friend-search"]'), 'mara')
    await wrapper.vm.$nextTick()

    const picker = element('[data-testid="goal-friend-picker"]')
    expect(picker.textContent).toContain('Mara Beispiel')
    expect(picker.textContent).not.toContain('Freund 2')

    type(element<HTMLInputElement>('[data-testid="goal-friend-search"] input, input[data-testid="goal-friend-search"]'), 'niemand')
    await wrapper.vm.$nextTick()
    expect(picker.textContent).toContain('Niemand mit diesem Namen.')
    wrapper.unmount()
  })

  it('offers the invite link instead of a form nobody could check', async () => {
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: [] },
      attachTo: document.body,
    })

    expect(element('[data-testid="goal-create-no-friends"]').textContent).toContain('Lade zuerst einen Freund ein')
    expect(element('[data-testid="invite-card"]')).toBeTruthy()
    expect(document.querySelector('[data-testid="goal-create-form"]')).toBeNull()
    wrapper.unmount()
  })

  it('never leaves a weekday schedule with no day in it', async () => {
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: [friend()] },
      attachTo: document.body,
    })

    type(element<HTMLInputElement>('[data-testid="goal-title-input"]'), 'Laufen')
    await wrapper.vm.$nextTick()
    await next(wrapper)

    element<HTMLButtonElement>('[data-testid="schedule-kind-Weekdays"]').click()
    await wrapper.vm.$nextTick()

    const monday = element<HTMLButtonElement>('[data-testid="schedule-weekday-Monday"]')
    expect(monday.getAttribute('aria-checked')).toBe('true')

    // Taking the last one away would leave a form the server refuses with
    // nothing on screen to say why.
    monday.click()
    await wrapper.vm.$nextTick()
    expect(monday.getAttribute('aria-checked')).toBe('true')

    wrapper.unmount()
  })

  it('matches field errors case-insensitively and takes a refusal back to its step', async () => {
    const fieldFailure: ApiFailure = {
      kind: 'validation',
      isExpected: true,
      status: 400,
      fieldErrors: { Title: ['Titel fehlt'], ICON: ['Icon ungültig'] },
      traceId: null,
      errorId: null,
      reason: null,
    }
    const fieldWrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: [friend()], error: fieldFailure },
      attachTo: document.body,
    })

    expect(document.body.textContent).toContain('Titel fehlt')
    expect(document.body.textContent).toContain('Icon ungültig')
    expect(document.querySelector('[data-testid="goal-create-error"]')).toBeNull()
    fieldWrapper.unmount()
    document.body.replaceChildren()

    // Refused on the summary for want of a friend: back to the friends.
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: [friend()] },
      attachTo: document.body,
    })
    type(element<HTMLInputElement>('[data-testid="goal-title-input"]'), 'Laufen')
    await wrapper.vm.$nextTick()
    await next(wrapper)
    await next(wrapper)
    element<HTMLElement>('[data-testid="goal-friend-person-1"] [role="checkbox"]').click()
    await wrapper.vm.$nextTick()
    await next(wrapper)

    await wrapper.setProps({ error: { ...fieldFailure, fieldErrors: { participantIds: ['Freund fehlt'] } } })
    expect(element('[data-testid="goal-create-step"]').textContent).toContain('Schritt 3 von 4')
    expect(document.body.textContent).toContain('Freund fehlt')
    wrapper.unmount()
    document.body.replaceChildren()

    const requestFailure = { ...fieldFailure, kind: 'network' as const, fieldErrors: {} }
    const requestWrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: [friend()], error: requestFailure },
      attachTo: document.body,
    })
    expect(element('[data-testid="goal-create-error"]').getAttribute('role')).toBe('alert')
    requestWrapper.unmount()
  })
})

describe('ChatCreateSheet', () => {
  it('starts direct and emits the selected friend', async () => {
    const wrapper = await mountSuspended(ChatCreateSheet, {
      props: { open: true, friends: [friend()], submitting: false },
      attachTo: document.body,
    })

    element<HTMLButtonElement>('[data-testid="chat-with-person-1"]').click()
    await wrapper.vm.$nextTick()
    expect(wrapper.emitted('direct')?.[0]).toEqual(['person-1'])
    wrapper.unmount()
  })

  it('requires a title and member before emitting a trimmed group', async () => {
    const wrapper = await mountSuspended(ChatCreateSheet, {
      props: { open: true, friends: [friend()], submitting: false },
      attachTo: document.body,
    })

    element<HTMLButtonElement>('[data-testid="chat-mode-group"]').click()
    await wrapper.vm.$nextTick()
    const submit = element<HTMLButtonElement>('[data-testid="group-create-submit"]')
    expect(submit.disabled).toBe(true)

    type(element<HTMLInputElement>('[data-testid="group-title-input"]'), '  Laufgruppe  ')
    element<HTMLButtonElement>('[data-testid="group-icon-sprout"]').click()
    element<HTMLElement>('[data-testid="group-member-person-1"] [role="checkbox"]').click()
    await wrapper.vm.$nextTick()
    expect(submit.disabled).toBe(false)

    element<HTMLFormElement>('[data-testid="group-create-form"]')
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
    await wrapper.vm.$nextTick()
    expect(wrapper.emitted('group')?.[0]).toEqual([{
      title: 'Laufgruppe',
      icon: 'sprout',
      memberIds: ['person-1'],
    }])
    wrapper.unmount()
  })

  it('shows an honest empty state when there are no friends', async () => {
    const wrapper = await mountSuspended(ChatCreateSheet, {
      props: { open: true, friends: [], submitting: false },
      attachTo: document.body,
    })

    expect(element('[data-testid="chat-create-no-friends"]').getAttribute('role')).toBe('status')
    expect(document.querySelector('[data-testid="chat-friend-picker"]')).toBeNull()
    wrapper.unmount()
  })
})
