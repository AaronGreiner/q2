import { mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, describe, expect, it } from 'vitest'
import ChatCreateSheet from '~/components/chats/ChatCreateSheet.vue'
import GoalCreateSheet from '~/components/goals/GoalCreateSheet.vue'
import type { ApiFailure } from '~/api/errors'
import type { Friend, Person } from '~/api/types'
import { addDays, formatDay } from '~/utils/display'

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
    element<HTMLButtonElement>('[data-testid="category-Sport"]').click()
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
    // What the first window asks, and until when — its share, if the week
    // has already begun (#96).
    expect(element('[data-testid="goal-first-window"]').textContent).toMatch(/Dein erstes Fenster: \d Nachweise? bis/)

    element<HTMLButtonElement>('[data-testid="goal-submit"]').click()
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('submit')?.[0]).toEqual([{
      title: 'Neues Ziel',
      schedule: { kind: 'Times', times: 3, period: 'Week' },
      category: 'Sport',
      reminderAt: '07:15:00',
      participantIds: ['person-1'],
    }])
    wrapper.unmount()
  })

  /**
   * The API took a description all along, but nothing sent one, so friends
   * voted on a title alone (#95). It takes the ideas' place once there is a
   * title, and is left out of the request when nothing was said — the first
   * test above pins that.
   */
  it('lets the owner say what counts, shows it in the summary and sends it trimmed', async () => {
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: [friend()] },
      attachTo: document.body,
    })

    const field = () => document.querySelector<HTMLTextAreaElement>(
      'textarea[data-testid="goal-description-input"], [data-testid="goal-description-input"] textarea')

    // Ideas first; nothing to describe before there is something to do.
    expect(field()).toBeNull()

    type(element<HTMLInputElement>('[data-testid="goal-title-input"]'), 'Laufen')
    await wrapper.vm.$nextTick()

    type(field()!, '  Mindestens 5 km, ohne Pause.  ')
    await wrapper.vm.$nextTick()
    await next(wrapper)
    await next(wrapper)
    element<HTMLElement>('[data-testid="goal-friend-person-1"] [role="checkbox"]').click()
    await wrapper.vm.$nextTick()
    await next(wrapper)

    expect(element('[data-testid="goal-review-description"]').textContent).toContain('Mindestens 5 km, ohne Pause.')

    element<HTMLButtonElement>('[data-testid="goal-submit"]').click()
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({
      title: 'Laufen',
      description: 'Mindestens 5 km, ohne Pause.',
    })
    wrapper.unmount()
  })

  /**
   * "Einmalig" used to say "bis zum gewählten Tag" with nothing to choose a
   * day with, and was due the day it was made (#94).
   */
  it('gives a one-off its day, says it in the summary and sends it', async () => {
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: [friend()] },
      attachTo: document.body,
    })

    type(element<HTMLInputElement>('[data-testid="goal-title-input"]'), 'Steuererklärung abgeben')
    await wrapper.vm.$nextTick()
    await next(wrapper)

    element<HTMLButtonElement>('[data-testid="schedule-kind-Once"]').click()
    await wrapper.vm.$nextTick()

    // From today, a year ahead at most, and today until somebody changes it.
    const day = element<HTMLInputElement>('input[data-testid="schedule-target-date"], [data-testid="schedule-target-date"] input')
    const today = day.getAttribute('min')!
    expect(day.type).toBe('date')
    expect(day.value).toBe(today)
    expect(day.getAttribute('max')).toBe(addDays(today, 365))

    const later = addDays(today, 10)
    type(day, later)
    await wrapper.vm.$nextTick()
    expect(element('[data-testid="schedule-preview"]').textContent).toContain(`Einmalig · bis ${formatDay(later)}`)

    await next(wrapper)
    element<HTMLElement>('[data-testid="goal-friend-person-1"] [role="checkbox"]').click()
    await wrapper.vm.$nextTick()
    await next(wrapper)

    expect(element('[data-testid="goal-create-review"]').textContent).toContain(`bis ${formatDay(later)}`)
    expect(element('[data-testid="goal-first-window"]').textContent).toContain(`${formatDay(later)} um Mitternacht`)

    element<HTMLButtonElement>('[data-testid="goal-submit"]').click()
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({
      schedule: { kind: 'Once' },
      targetDate: later,
    })
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

  it('fills the title, the category and the rhythm from an idea', async () => {
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: [friend()] },
      attachTo: document.body,
    })

    element<HTMLButtonElement>('[data-testid="goal-template-tidy"]').click()
    await wrapper.vm.$nextTick()

    expect(element<HTMLInputElement>('[data-testid="goal-title-input"]').value).toBe('Jeden Sonntag aufräumen')
    expect(element('[data-testid="category-Other"]').getAttribute('aria-checked')).toBe('true')

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

  it('lets somebody without friends keep a Do waiting rather than refusing them (#93)', async () => {
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: [] },
      attachTo: document.body,
    })

    expect(element('[data-testid="goal-create-draft"]').textContent).toContain('Merk dein Do schon vor')
    expect(document.querySelector('[data-testid="goal-create-form"]')).toBeNull()

    // Nothing to keep waiting until there is a title.
    expect(element<HTMLButtonElement>('[data-testid="goal-draft-submit"]').disabled).toBe(true)

    element<HTMLButtonElement>('[data-testid="draft-idea-read"]').click()
    await wrapper.vm.$nextTick()
    await wrapper.vm.$nextTick()

    const submit = element<HTMLButtonElement>('[data-testid="goal-draft-submit"]')
    expect(submit.disabled).toBe(false)
    submit.click()
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('draft')).toEqual([[{
      title: 'Jeden Tag lesen',
      category: 'Reading',
      schedule: { kind: 'Interval', everyDays: 1 },
      reminderAt: '19:00:00',
    }]])
    expect(wrapper.emitted('submit')).toBeUndefined()
    wrapper.unmount()
  })

  it('offers the invite link once a Do is already waiting', async () => {
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: {
        open: true,
        friends: [],
        draft: {
          id: 'draft-1',
          title: 'Jeden Tag lesen',
          category: 'Reading',
          icon: 'book-open',
          schedule: { kind: 'Interval', everyDays: 1, weekdays: [], times: null, period: null },
          reminderAt: null,
          createdAt: '2026-10-12T09:00:00Z',
        },
      },
      attachTo: document.body,
    })

    expect(element('[data-testid="goal-create-no-friends"]').textContent).toContain('Du hast schon ein Do vorgemerkt')
    expect(element('[data-testid="invite-card"]')).toBeTruthy()
    expect(document.querySelector('[data-testid="goal-draft-form"]')).toBeNull()
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
    // The server's messages are English; what is shown is worded from the
    // codes beside them, in the screen's language (#80).
    const fieldFailure: ApiFailure = {
      kind: 'validation',
      isExpected: true,
      status: 400,
      fieldErrors: { Title: ['A title may be at most 120 characters long.'], CATEGORY: ['That is not one of the goal categories.'] },
      fieldCodes: { Title: [{ code: 'tooLong', max: 120 }], CATEGORY: [{ code: 'unknownOption' }] },
      traceId: null,
      errorId: null,
      reason: null,
    }
    const fieldWrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: [friend()], error: fieldFailure },
      attachTo: document.body,
    })

    expect(document.body.textContent).toContain('Höchstens 120 Zeichen.')
    expect(document.body.textContent).toContain('Bitte wähl eine der Möglichkeiten.')
    expect(document.body.textContent).not.toContain('at most')
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

    await wrapper.setProps({
      error: {
        ...fieldFailure,
        fieldErrors: { ParticipantIds: ['Invite at least one friend to check this goal.'] },
        fieldCodes: { ParticipantIds: [{ code: 'noChecker' }] },
      },
    })
    expect(element('[data-testid="goal-create-step"]').textContent).toContain('Schritt 3 von 4')
    expect(document.body.textContent).toContain('Wähl mindestens eine Person, die dich prüft.')
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

describe('GoalCreateSheet, starting tomorrow (#96)', () => {
  function el<T extends Element = HTMLElement>(selector: string): T {
    const found = document.querySelector<T>(selector)
    if (!found) throw new Error(`Nothing matches ${selector}`)
    return found
  }

  it('offers today or tomorrow on the summary, and sends the day it starts', async () => {
    const wrapper = await mountSuspended(GoalCreateSheet, {
      props: { open: true, friends: [friend()] },
      attachTo: document.body,
    })

    const title = el<HTMLInputElement>('[data-testid="goal-title-input"]')
    title.value = 'Jeden Tag lesen'
    title.dispatchEvent(new Event('input'))
    await wrapper.vm.$nextTick()

    for (let step = 0; step < 2; step++) {
      el<HTMLButtonElement>('[data-testid="goal-next"]').click()
      await wrapper.vm.$nextTick()
    }
    el<HTMLElement>('[data-testid="goal-friend-person-1"] [role="checkbox"]').click()
    await wrapper.vm.$nextTick()
    el<HTMLButtonElement>('[data-testid="goal-next"]').click()
    await wrapper.vm.$nextTick()

    expect(el('[data-testid="goal-start"]').textContent).toContain('Ab morgen')
    el<HTMLButtonElement>('[data-testid="segment-tomorrow"]').click()
    await wrapper.vm.$nextTick()

    expect(el('[data-testid="goal-first-window"]').textContent).toContain('1 Nachweis bis morgen um Mitternacht.')
    expect(el('[data-testid="goal-first-window"]').textContent).toContain('Heute ist noch nichts fällig.')

    el<HTMLButtonElement>('[data-testid="goal-submit"]').click()
    await wrapper.vm.$nextTick()

    const [request] = wrapper.emitted('submit')![0] as [Record<string, unknown>]
    expect(request.startsOn).toMatch(/^\d{4}-\d{2}-\d{2}$/)
    wrapper.unmount()
    document.body.replaceChildren()
  })
})
