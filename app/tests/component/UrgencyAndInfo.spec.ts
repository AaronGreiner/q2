import { mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, describe, expect, it } from 'vitest'
import ChatInfoSheet from '~/components/chats/ChatInfoSheet.vue'
import ChatListRow from '~/components/chats/ChatListRow.vue'
import ChatKeptRun from '~/components/chats/ChatKeptRun.vue'
import ChatMissedRun from '~/components/chats/ChatMissedRun.vue'
import GoalReminderSheet from '~/components/goals/GoalReminderSheet.vue'
import NextUpCard from '~/components/home/NextUpCard.vue'
import type { ChatDetail, ChatSummary, Goal, GoalWindow, Person } from '~/api/types'

/**
 * What came with making the next step obvious: the card at the top of the
 * start screen, the chat list saying what is due instead of what was missed,
 * the folded run of misses, the sheet behind a conversation's header, and
 * moving a reminder.
 */

const noon = Date.parse('2026-09-24T12:00:00Z')

function window(overrides: Partial<GoalWindow> = {}): GoalWindow {
  return {
    id: 'w',
    startsOn: '2026-09-24',
    dueOn: '2026-09-24',
    dueAt: '2026-09-24T22:00:00Z',
    requiredProofs: 1,
    confirmedProofs: 0,
    remainingProofs: 1,
    status: 'Open',
    pendingProofId: null,
    acceptsProof: true,
    ...overrides,
  }
}

function person(overrides: Partial<Person> = {}): Person {
  return {
    id: 'person-1',
    displayName: 'Mara Klein',
    handle: 'mara.k',
    initials: 'MK',
    avatarColor: '#4f46e5',
    avatarImageId: null,
    isOnline: false,
    ...overrides,
  }
}

function element<T extends Element>(selector: string): T {
  const found = document.querySelector<T>(selector)
  expect(found, `Expected ${selector} to be rendered by the open drawer`).not.toBeNull()
  return found!
}

afterEach(() => {
  document.body.replaceChildren()
})

describe('NextUpCard', () => {
  const goal = { id: 'goal-1', title: 'Jeden Tag lesen', icon: 'book-open', current: window() } as Goal

  it('says what is next, how long is left, and offers the camera', async () => {
    const wrapper = await mountSuspended(NextUpCard, { props: { goal, now: noon } })

    expect(wrapper.text()).toContain('Als Nächstes')
    expect(wrapper.text()).toContain('Jeden Tag lesen')
    expect(wrapper.get('[data-testid="next-up-when"]').text()).toBe('Heute fällig · noch 10 Std.')
    expect(wrapper.get('a').attributes('href')).toBe('/goals/goal-1')

    await wrapper.get('[data-testid="next-up-deliver"]').trigger('click')
    expect(wrapper.emitted('deliver')?.[0]).toEqual(['goal-1'])
  })

  it('keeps the title out of Session Replay', async () => {
    const wrapper = await mountSuspended(NextUpCard, { props: { goal, now: noon } })

    expect(wrapper.get('[data-q2-private]').text()).toContain('Jeden Tag lesen')
  })
})

describe('ChatListRow — your own goal', () => {
  const row = {
    id: 'chat-1',
    kind: 'Goal',
    name: 'Frühaufsteher',
    initials: 'FR',
    icon: 'sunrise',
    avatarColor: '#4f46e5',
    avatarImageId: null,
    isOnline: false,
    lastMessage: null,
    lastMessageSenderName: null,
    lastMessageHasPhoto: false,
    lastMessageIsMine: false,
    lastMessageIsDeleted: false,
    lastMessageAt: '2026-09-20T09:00:00Z',
    unreadCount: 0,
    isMuted: false,
    isPinned: false,
    lastEvent: 'WindowDone',
    goalId: 'goal-1',
    isMyGoal: true,
    awaitingMyVote: false,
    goalCurrent: window(),
  } as ChatSummary

  it('says what is due next rather than repeating what happened', async () => {
    const wrapper = await mountSuspended(ChatListRow, { props: { chat: row, now: noon } })

    expect(wrapper.text()).toContain('Heute fällig · noch 10 Std.')
    expect(wrapper.text()).not.toContain('Geschafft')
  })

  it('lets something newer that somebody wrote speak for itself', async () => {
    const written = { ...row, lastEvent: null, lastMessage: 'Guten Morgen', lastMessageIsMine: true }
    const wrapper = await mountSuspended(ChatListRow, { props: { chat: written, now: noon } })

    expect(wrapper.text()).toContain('Du: Guten Morgen')
  })

  it('falls back to what happened when there is nothing to deliver now', async () => {
    const waiting = { ...row, goalCurrent: window({ acceptsProof: false, pendingProofId: 'p' }) }
    const wrapper = await mountSuspended(ChatListRow, { props: { chat: waiting, now: noon } })

    expect(wrapper.text()).toContain('Geschafft')
  })
})

describe('ChatMissedRun', () => {
  it('says the days and the count once', async () => {
    const wrapper = await mountSuspended(ChatMissedRun, { props: { from: '7.9.', to: '23.9.', count: 15 } })

    expect(wrapper.text()).toBe('7.9.–23.9. · 15 Fenster verpasst')
    expect(wrapper.get('[data-q2-private]').exists()).toBe(true)
  })
})

describe('ChatKeptRun', () => {
  it('says the days, the count and the streak it ends on, once (#100)', async () => {
    const done = await mountSuspended(ChatKeptRun, { props: { from: '22.9.', to: '12.10.', count: 21, streak: 21, held: false } })

    expect(done.text()).toBe('22.9.–12.10. · 21 Fenster geschafft · Streak 21')
    expect(done.get('[data-q2-private]').exists()).toBe(true)

    const held = await mountSuspended(ChatKeptRun, { props: { from: '1.10.', to: '7.10.', count: 7, streak: 7, held: true } })
    expect(held.text()).toBe('1.10.–7.10. · 7-mal durchgehalten · Streak 7')
  })
})

describe('ChatInfoSheet', () => {
  function chat(overrides: Partial<ChatDetail> = {}): ChatDetail {
    return {
      id: 'chat-1',
      kind: 'Goal',
      name: 'Frühaufsteher',
      initials: 'FR',
      icon: 'sunrise',
      avatarColor: '#4f46e5',
      avatarImageId: null,
      isOnline: false,
      memberCount: 2,
      otherLastSeenAt: null,
      pinnedGoal: { id: 'goal-1', title: 'Frühaufsteher', current: null, streak: 0, pausedUntil: null, isMine: true },
      messages: [],
      isMuted: false,
      isPinned: false,
      events: [],
      members: [person(), person({ id: 'person-2', displayName: 'Lena Schulz', handle: 'lena' })],
      ...overrides,
    }
  }

  it('lists the members, the reader as "Du" and everybody else as a way to their profile', async () => {
    const wrapper = await mountSuspended(ChatInfoSheet, {
      props: { open: true, chat: chat(), meId: 'person-1' },
      attachTo: document.body,
    })

    const members = document.querySelectorAll('[data-testid="chat-info-member"]')
    expect(members).toHaveLength(2)
    expect(members[0]!.textContent).toContain('Du')
    expect(members[0]!.querySelector('a')).toBeNull()
    expect(members[1]!.querySelector('a')?.getAttribute('href')).toBe('/people/person-2')
    expect(element('[data-testid="chat-info"] ul').hasAttribute('data-q2-private')).toBe(true)

    expect(element('[data-testid="chat-info-goal"]').getAttribute('href')).toBe('/goals/goal-1')
    expect(document.querySelector('[data-testid="leave-group"]')).toBeNull()
    wrapper.unmount()
  })

  it('offers leaving only in a group, and only asks for it', async () => {
    const wrapper = await mountSuspended(ChatInfoSheet, {
      props: { open: true, chat: chat({ kind: 'Group', pinnedGoal: null }), meId: 'person-1' },
      attachTo: document.body,
    })

    expect(document.querySelector('[data-testid="chat-info-goal"]')).toBeNull()
    element<HTMLButtonElement>('[data-testid="leave-group"]').click()
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('leave')).toHaveLength(1)
    wrapper.unmount()
  })

  /** The photographs and the pin (#24). */
  it('shows the photographs in a grid, and says so when there are none', async () => {
    const photo = { id: 'message-1', imageId: 'image-1', isProof: false, authorName: 'Lena Schulz', at: '2026-10-18T09:00:00Z', source: null }
    const wrapper = await mountSuspended(ChatInfoSheet, {
      props: { open: true, chat: chat(), meId: 'person-1', photos: [photo, { ...photo, id: 'message-2', imageId: 'image-2' }] },
      attachTo: document.body,
    })

    const tiles = document.querySelectorAll('[data-testid="chat-photo-tile"]')
    expect(tiles).toHaveLength(2)
    expect(tiles[0]!.getAttribute('aria-label')).toBe('Foto von Lena Schulz')
    expect(tiles[0]!.hasAttribute('data-q2-block')).toBe(true)
    wrapper.unmount()

    const empty = await mountSuspended(ChatInfoSheet, {
      props: { open: true, chat: chat(), meId: 'person-1', photos: [] },
      attachTo: document.body,
    })
    expect(element('[data-testid="chat-info-photos"]').textContent).toContain('Noch keine Fotos in diesem Chat.')
    empty.unmount()
  })

  it('pins with its switch, and says why when the server would not', async () => {
    const wrapper = await mountSuspended(ChatInfoSheet, {
      props: { open: true, chat: chat(), meId: 'person-1', photos: [] },
      attachTo: document.body,
    })

    element<HTMLButtonElement>('[data-testid="chat-info-pin"] button').click()
    await wrapper.vm.$nextTick()
    expect(wrapper.emitted('pin')?.[0]).toEqual([true])

    await wrapper.setProps({
      pinFailure: {
        kind: 'validation',
        status: 400,
        fieldErrors: { Pinned: ['At most 3 conversations can be pinned.'] },
        fieldCodes: { Pinned: [{ code: 'tooMany', max: 3 }] },
        reason: null,
        traceId: null,
        errorId: null,
        isExpected: true,
      },
    })
    expect(element('[data-testid="chat-info-pin-hint"]').textContent)
      .toBe('Höchstens 3 Chats lassen sich anheften. Löse zuerst einen anderen.')
    wrapper.unmount()
  })
})

describe('GoalReminderSheet', () => {
  function type(input: HTMLInputElement, value: string) {
    input.value = value
    input.dispatchEvent(new Event('input', { bubbles: true }))
  }

  it('starts from the stored time and saves a new one', async () => {
    const wrapper = await mountSuspended(GoalReminderSheet, {
      props: { open: true, current: '06:00:00' },
      attachTo: document.body,
    })

    const input = element<HTMLInputElement>('[data-testid="goal-reminder-time"] input, input[data-testid="goal-reminder-time"]')
    expect(input.value).toBe('06:00')

    type(input, '07:45')
    await wrapper.vm.$nextTick()
    element<HTMLButtonElement>('[data-testid="goal-reminder-save"]').click()
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('save')?.[0]).toEqual(['07:45:00'])
    wrapper.unmount()
  })

  it('takes the reminder away, and offers that only when there is one', async () => {
    const withOne = await mountSuspended(GoalReminderSheet, {
      props: { open: true, current: '06:00:00' },
      attachTo: document.body,
    })

    element<HTMLButtonElement>('[data-testid="goal-reminder-remove"]').click()
    await withOne.vm.$nextTick()
    expect(withOne.emitted('save')?.[0]).toEqual([null])
    withOne.unmount()
    document.body.replaceChildren()

    const without = await mountSuspended(GoalReminderSheet, {
      props: { open: true, current: null },
      attachTo: document.body,
    })
    expect(document.querySelector('[data-testid="goal-reminder-remove"]')).toBeNull()
    expect(element<HTMLInputElement>('[data-testid="goal-reminder-time"] input, input[data-testid="goal-reminder-time"]').value).toBe('09:00')
    without.unmount()
  })
})
