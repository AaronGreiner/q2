import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import AppBottomNav from '~/components/layout/AppBottomNav.vue'
import AppConfirmDialog from '~/components/ui/AppConfirmDialog.vue'
import AppProgressRing from '~/components/ui/AppProgressRing.vue'
import AppSearchField from '~/components/ui/AppSearchField.vue'
import AppSegmented from '~/components/ui/AppSegmented.vue'
import AppToggle from '~/components/ui/AppToggle.vue'
import BadgeGrid from '~/components/profile/BadgeGrid.vue'
import ChatBubble from '~/components/chats/ChatBubble.vue'
import ChatComposer from '~/components/chats/ChatComposer.vue'
import ChatGoalBanner from '~/components/chats/ChatGoalBanner.vue'
import ChatListRow from '~/components/chats/ChatListRow.vue'
import FriendRequestRow from '~/components/friends/FriendRequestRow.vue'
import FriendSuggestionRow from '~/components/friends/FriendSuggestionRow.vue'
import GoalTile from '~/components/goals/GoalTile.vue'
import SettingsActionRow from '~/components/settings/SettingsActionRow.vue'
import SettingsSection from '~/components/settings/SettingsSection.vue'
import SettingsToggleRow from '~/components/settings/SettingsToggleRow.vue'
import StreakHero from '~/components/home/StreakHero.vue'
import TodayProgressCard from '~/components/home/TodayProgressCard.vue'
import type {
  Badge,
  ChatMessage,
  ChatPinnedGoal,
  ChatSummary,
  FriendRequest,
  FriendSuggestion,
  Goal,
  Person,
} from '~/api/types'

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

function goal(): Goal {
  return {
    id: 'goal-1',
    title: 'Halbmarathon',
    description: null,
    icon: 'medal',
    schedule: { kind: 'Times', everyDays: null, weekdays: [], times: 3, period: 'Week' },
    status: 'Active',
    isGroup: true,
    current: {
      id: 'window-1',
      startsOn: '2026-07-27',
      dueOn: '2026-08-02',
      dueAt: '2026-08-02T21:59:59Z',
      requiredProofs: 3,
      confirmedProofs: 1,
      remainingProofs: 2,
      status: 'Open',
    },
    streak: 2,
    windowsDone: 6,
    windowsMissed: 1,
    reminderAt: null,
    targetDate: null,
    createdAt: '2026-07-31T09:00:00Z',
    participants: [person()],
    isOverdue: false,
    isMine: true,
    closedAt: null,
    pause: null,
    remainingPauses: 2,
  }
}

describe('interactive UI primitives', () => {
  it('clamps and labels a progress ring while exposing its slot value', async () => {
    const wrapper = await mountSuspended(AppProgressRing, {
      props: { percent: 140, size: 100, label: 'Fortschritt' },
      slots: { default: '<span data-testid="ring-value">100</span>' },
    })

    expect(wrapper.attributes('aria-valuenow')).toBe('100')
    expect(wrapper.attributes('aria-label')).toBe('Fortschritt')
    expect(wrapper.attributes('style')).toContain('width: 100px')
    expect(wrapper.find('[data-testid="ring-value"]').exists()).toBe(true)
  })

  it('updates the shared search model through a genuinely labelled field', async () => {
    const wrapper = await mountSuspended(AppSearchField, {
      props: {
        'id': 'people-search',
        'label': 'Person suchen',
        'placeholder': 'Name',
        'modelValue': '',
        'testId': 'people-search-input',
        'onUpdate:modelValue': (value: string) => wrapper.setProps({ modelValue: value }),
      },
    })

    const input = wrapper.get('input')
    await input.setValue('Mara')
    expect(wrapper.get('label').attributes('for')).toBe('people-search')
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['Mara'])
  })

  it('operates segmented controls and switches with their native ARIA roles', async () => {
    const segmented = await mountSuspended(AppSegmented, {
      props: {
        label: 'Ansicht',
        options: [{ value: 'today', label: 'Heute' }, { value: 'goals', label: 'Ziele' }],
        modelValue: 'today',
      },
    })
    await segmented.get('[data-testid="segment-goals"]').trigger('click')
    expect(segmented.emitted('update:modelValue')?.[0]).toEqual(['goals'])
    expect(segmented.get('[data-testid="segment-today"]').attributes('role')).toBe('radio')

    const toggle = await mountSuspended(AppToggle, { props: { label: 'Erinnerungen', modelValue: false } })
    await toggle.get('[role="switch"]').trigger('click')
    expect(toggle.emitted('update:modelValue')?.[0]).toEqual([true])
  })

  it('closes and emits a destructive confirmation', async () => {
    const wrapper = await mountSuspended(AppConfirmDialog, {
      props: {
        open: true,
        title: 'Gruppe verlassen',
        description: 'Diese Gruppe wirklich verlassen?',
        confirmLabel: 'Verlassen',
        privateDescription: true,
      },
      attachTo: document.body,
    })

    const accept = document.querySelector<HTMLButtonElement>('[data-testid="confirm-accept"]')
    expect(accept).not.toBeNull()
    accept!.click()
    await wrapper.vm.$nextTick()
    expect(wrapper.emitted('confirm')).toHaveLength(1)
    expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
    wrapper.unmount()
  })
})

describe('chat presentation', () => {
  function message(overrides: Partial<ChatMessage> = {}): ChatMessage {
    return {
      id: 'message-1',
      senderId: 'person-1',
      senderName: 'Mara',
      text: 'Du schaffst das!',
      sentAt: '2026-07-31T09:00:00Z',
      isMine: false,
      reactions: [{ kind: 'Applause' as const, count: 2, isMine: true }],
      ...overrides,
    }
  }

  it('renders a private message and offers all three kinds of kudos', async () => {
    const wrapper = await mountSuspended(ChatBubble, {
      props: { message: message(), now: Date.parse('2026-07-31T10:00:00Z') },
    })

    expect(wrapper.text()).toContain('Du schaffst das!')
    expect(wrapper.attributes()).toHaveProperty('data-q2-block')

    // The one already given is pressed; the other two are there to be given.
    expect(wrapper.get('[data-testid="chat-kudos-Applause"]').attributes('aria-pressed')).toBe('true')
    expect(wrapper.get('[data-testid="chat-kudos-Fire"]').attributes('aria-pressed')).toBe('false')

    // An icon is not a name: each button says which kudos it gives.
    expect(wrapper.get('[data-testid="chat-kudos-Fire"]').attributes('aria-label')).toBe('Stark gemacht')

    await wrapper.get('[data-testid="chat-kudos-Fire"]').trigger('click')
    expect(wrapper.emitted('react')?.[0]).toEqual(['message-1', 'Fire'])
  })

  it('does not offer a reaction on your own message', async () => {
    const wrapper = await mountSuspended(ChatBubble, {
      props: { message: message({ isMine: true, senderName: null }), now: Date.now() },
    })
    expect(wrapper.find('[data-testid="chat-kudos-Fire"]').exists()).toBe(false)
  })

  it('trims a composed message, clears the field and offers quick cheers', async () => {
    const wrapper = await mountSuspended(ChatComposer)
    const input = wrapper.get('[data-testid="chat-input"]')

    await input.setValue('  Hallo  ')
    await wrapper.get('form').trigger('submit')
    expect(wrapper.emitted('send')?.[0]).toEqual(['Hallo'])
    expect((input.element as HTMLInputElement).value).toBe('')

    await wrapper.get('[data-testid="quick-cheer"]').trigger('click')
    expect(wrapper.emitted('send')).toHaveLength(2)
  })

  it('links a pinned goal and emits encouragement', async () => {
    const pinned = {
      id: 'goal-1',
      title: 'Halbmarathon',
      current: goal().current,
      streak: 2,
    } as ChatPinnedGoal
    const wrapper = await mountSuspended(ChatGoalBanner, { props: { goal: pinned } })

    expect(wrapper.get('a').attributes('href')).toBe('/goals/goal-1')

    // What the window still wants, not a percentage of a counter.
    expect(wrapper.text()).toContain('Noch 2 von 3')
    await wrapper.get('[data-testid="chat-cheer"]').trigger('click')
    expect(wrapper.emitted('cheer')).toHaveLength(1)
  })

  it('renders unread state and an honest empty preview in chat rows', async () => {
    const summary = {
      id: 'chat-1',
      name: 'Jonas',
      initials: 'JW',
      avatarColor: '#4f46e5',
      avatarImageId: null,
      isOnline: true,
      isGroup: false,
      lastMessage: 'Bis morgen',
      lastMessageAt: '2026-07-31T09:00:00Z',
      lastMessageSenderName: 'Jonas',
      lastMessageIsMine: false,
      unreadCount: 2,
    } as ChatSummary
    const unread = await mountSuspended(ChatListRow, { props: { chat: summary, now: Date.now() } })
    expect(unread.text()).toContain('Jonas: Bis morgen')
    expect(unread.text()).toContain('2')

    const empty = await mountSuspended(ChatListRow, {
      props: { chat: { ...summary, lastMessage: null, lastMessageAt: null, unreadCount: 0 }, now: Date.now() },
    })
    expect(empty.text()).toContain('Noch keine Nachrichten')
  })
})

describe('feature cards and rows', () => {
  it('renders the week, day progress and a compact private goal', async () => {
    const streak = await mountSuspended(StreakHero, {
      props: { streak: 5, week: [true, true, false, true, false, true, true] },
    })
    expect(streak.findAll('li')).toHaveLength(7)
    expect(streak.text()).toContain('5')

    const today = await mountSuspended(TodayProgressCard, {
      props: { today: { done: 2, total: 4, percent: 50 }, streak: 5 },
    })
    expect(today.get('[role="progressbar"]').attributes('aria-valuenow')).toBe('50')

    const tile = await mountSuspended(GoalTile, { props: { goal: goal() } })
    expect(tile.get('a').attributes('href')).toBe('/goals/goal-1')
    expect(tile.findAll('[data-testid="avatar"]')).toHaveLength(1)
    expect(tile.text()).toContain('3× pro Woche')
    expect(tile.text()).toContain('Noch 2 von 3')
  })

  it('emits both answers to a friend request and a suggestion', async () => {
    const request = {
      person: person({ id: 'request-1', displayName: 'Emma' }),
      requestedAt: '2026-07-31T09:00:00Z',
      mutualFriends: 2,
    } as FriendRequest
    const requestRow = await mountSuspended(FriendRequestRow, { props: { request } })
    await requestRow.get('[data-testid="request-accept"]').trigger('click')
    await requestRow.get('[data-testid="request-decline"]').trigger('click')
    expect(requestRow.emitted('accept')?.[0]).toEqual(['request-1'])
    expect(requestRow.emitted('decline')?.[0]).toEqual(['request-1'])

    const suggestion = { person: person({ id: 'suggestion-1' }), mutualFriends: 3 } as FriendSuggestion
    const suggestionRow = await mountSuspended(FriendSuggestionRow, { props: { suggestion } })
    await suggestionRow.get('[data-testid="suggestion-request"]').trigger('click')
    expect(suggestionRow.emitted('request')?.[0]).toEqual(['suggestion-1'])
  })

  it('names earned and locked badges in words', async () => {
    const badges = [
      { key: 'StreakHero', isEarned: true, earnedAt: '2026-07-31T09:00:00Z' },
      { key: 'EarlyBird', isEarned: false, earnedAt: null },
    ] as Badge[]
    const wrapper = await mountSuspended(BadgeGrid, { props: { badges } })

    expect(wrapper.findAll('li')).toHaveLength(2)
    expect(wrapper.text()).toContain('noch nicht erreicht')
  })

  it('renders settings semantics and forwards switch changes', async () => {
    const section = await mountSuspended(SettingsSection, {
      props: { title: 'Benachrichtigungen', note: 'Noch nicht aktiv' },
      slots: { default: '<p>Inhalt</p>' },
    })
    expect(section.get('h2').text()).toBe('Benachrichtigungen')
    expect(section.text()).toContain('Noch nicht aktiv')

    const row = await mountSuspended(SettingsToggleRow, {
      props: { icon: 'i-lucide-bell', label: 'Erinnerungen', modelValue: false },
    })
    await row.get('[role="switch"]').trigger('click')
    expect(row.emitted('update:modelValue')?.[0]).toEqual([true])
  })

  it('asks a settings action row to act, and stops asking while it is busy', async () => {
    const row = await mountSuspended(SettingsActionRow, {
      props: { icon: 'i-lucide-message-square-heart', label: 'Feedback senden' },
    })

    // A real button, so it is reachable by keyboard and announces itself as one.
    const button = row.get('button')
    expect(button.attributes('type')).toBe('button')
    expect(button.text()).toContain('Feedback senden')

    await button.trigger('click')
    expect(row.emitted('activate')).toHaveLength(1)

    await row.setProps({ busy: true })
    expect(button.attributes('disabled')).toBeDefined()

    await button.trigger('click')
    expect(row.emitted('activate')).toHaveLength(1)
  })

  it('uses real destinations and shows both navigation counters', async () => {
    const wrapper = await mountSuspended(AppBottomNav, {
      props: { unreadChats: 3, pendingRequests: 2 },
      route: '/',
    })

    // Four destinations and the create button, which is a link too so that the
    // sheet it opens survives a reload and closes with the back gesture.
    expect(wrapper.findAll('a')).toHaveLength(5)
    expect(wrapper.get('[data-testid="nav-home"]').attributes('href')).toBe('/')
    expect(wrapper.get('[data-testid="nav-create"]').attributes('href')).toBe('/goals?create=1')
    expect(wrapper.get('[data-testid="nav-chats"]').text()).toContain('3')

    // Friend requests followed the friends screen into search.
    expect(wrapper.get('[data-testid="nav-search"]').text()).toContain('2')
  })
})
