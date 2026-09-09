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
  it('starts disabled and emits the complete trimmed default request', async () => {
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true },
      attachTo: document.body,
    })

    const submit = element<HTMLButtonElement>('[data-testid="goal-submit"]')
    expect(submit.disabled).toBe(true)

    type(element<HTMLInputElement>('[data-testid="goal-title-input"]'), '  Neues Ziel  ')
    element<HTMLButtonElement>('[data-testid="schedule-kind-Times"]').click()
    await wrapper.vm.$nextTick()
    element<HTMLButtonElement>('[data-testid="schedule-times-3"]').click()
    element<HTMLButtonElement>('[data-testid="icon-flame"]').click()
    await wrapper.vm.$nextTick()
    expect(submit.disabled).toBe(false)

    // The preview says what the choice means, in the words the goal will use.
    expect(element('[data-testid="schedule-preview"]').textContent).toContain('3× pro Woche')

    element<HTMLFormElement>('[data-testid="goal-create-form"]')
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('submit')?.[0]).toEqual([{
      title: 'Neues Ziel',
      schedule: { kind: 'Times', times: 3, period: 'Week' },
      icon: 'flame',
      reminderAt: '09:00:00',
    }])
    wrapper.unmount()
  })

  it('never leaves a weekday schedule with no day in it', async () => {
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true },
      attachTo: document.body,
    })

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

  it('matches field errors case-insensitively and separates request failures', async () => {
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
      props: { open: true, error: fieldFailure },
      attachTo: document.body,
    })

    expect(document.body.textContent).toContain('Titel fehlt')
    expect(document.body.textContent).toContain('Icon ungültig')
    expect(document.querySelector('[data-testid="goal-create-error"]')).toBeNull()
    fieldWrapper.unmount()
    document.body.replaceChildren()

    const requestFailure = { ...fieldFailure, kind: 'network' as const, fieldErrors: {} }
    const requestWrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, error: requestFailure },
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
