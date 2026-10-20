import { mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { hapticTap } from '~/utils/haptics'
import AppBottomNav from '~/components/layout/AppBottomNav.vue'
import AppConfirmDialog from '~/components/ui/AppConfirmDialog.vue'
import AppProgressRing from '~/components/ui/AppProgressRing.vue'
import AppSearchField from '~/components/ui/AppSearchField.vue'
import AppSegmented from '~/components/ui/AppSegmented.vue'
import AppToggle from '~/components/ui/AppToggle.vue'
import ChatBubble from '~/components/chats/ChatBubble.vue'
import ChatComposer from '~/components/chats/ChatComposer.vue'
import ChatGoalBanner from '~/components/chats/ChatGoalBanner.vue'
import ChatListRow from '~/components/chats/ChatListRow.vue'
import FriendRequestRow from '~/components/friends/FriendRequestRow.vue'
import FriendSuggestionRow from '~/components/friends/FriendSuggestionRow.vue'
import SentRequestRow from '~/components/friends/SentRequestRow.vue'
import SettingsActionRow from '~/components/settings/SettingsActionRow.vue'
import SettingsSection from '~/components/settings/SettingsSection.vue'
import SettingsToggleRow from '~/components/settings/SettingsToggleRow.vue'
import type {
  ChatMessage,
  ChatPinnedGoal,
  ChatSummary,
  FriendRequest,
  FriendSuggestion,
  Goal,
  Person,
  SentRequest,
} from '~/api/types'

vi.mock('~/utils/haptics', () => ({ hapticTap: vi.fn() }))
afterEach(() => vi.mocked(hapticTap).mockClear())

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
    category: 'Sport',
    icon: 'dumbbell',
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
        options: [{ value: 'today', label: 'Heute' }, { value: 'all', label: 'Alle' }],
        modelValue: 'today',
      },
    })
    await segmented.get('[data-testid="segment-all"]').trigger('click')
    expect(segmented.emitted('update:modelValue')?.[0]).toEqual(['all'])
    expect(segmented.findAll('[role="radio"]')).toHaveLength(2)

    const toggle = await mountSuspended(AppToggle, { props: { label: 'Erinnerungen', modelValue: false } })
    await toggle.get('[role="switch"]').trigger('click')
    expect(toggle.emitted('update:modelValue')?.[0]).toEqual([true])
  })

  it('names each segment by its label, and renames it when the language changes', async () => {
    // reka-ui looks the label up in `document`, so the control has to be in it.
    const segmented = await mountSuspended(AppSegmented, {
      props: {
        label: 'Design',
        options: [{ value: 'Light', label: 'Hell' }, { value: 'Dark', label: 'Dunkel' }],
        modelValue: 'Light',
      },
      attachTo: document.body,
    })
    await nextTick()
    // A browser's innerText trims the template's whitespace; happy-dom's does not.
    const names = () => segmented.findAll('[role="radio"]').map(radio => radio.attributes('aria-label')?.trim())
    expect(names()).toEqual(['Hell', 'Dunkel'])

    await segmented.setProps({ options: [{ value: 'Light', label: 'Light' }, { value: 'Dark', label: 'Dark' }] })
    await nextTick()
    expect(names()).toEqual(['Light', 'Dark'])
    segmented.unmount()
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
      image: null,
      sentAt: '2026-07-31T09:00:00Z',
      isMine: false,
      reactions: [{ kind: 'Applause' as const, count: 2, isMine: true }],
      editedAt: null,
      isDeleted: false,
      canEdit: false,
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
    expect(hapticTap).toHaveBeenCalledOnce()
  })

  /**
   * The menu is asked for, never opened here (#25): held on a phone,
   * right-clicked on a desktop, Enter from a keyboard.
   */
  it('asks for its menu when held, right-clicked or entered', async () => {
    vi.useFakeTimers()

    try {
      const wrapper = await mountSuspended(ChatBubble, { props: { message: message(), now: Date.now() } })
      const words = wrapper.get('[data-testid="chat-message-text"]')

      await words.trigger('pointerdown', { pointerType: 'touch', clientX: 10, clientY: 10 })
      vi.advanceTimersByTime(500)
      expect(wrapper.emitted('actions')).toHaveLength(1)

      // A scroll that starts on a message is not a press.
      await words.trigger('pointerdown', { pointerType: 'touch', clientX: 10, clientY: 10 })
      await words.trigger('pointermove', { pointerType: 'touch', clientX: 10, clientY: 60 })
      vi.advanceTimersByTime(500)
      expect(wrapper.emitted('actions')).toHaveLength(1)

      await words.trigger('contextmenu')
      await words.trigger('keydown', { key: 'Enter' })
      expect(wrapper.emitted('actions')).toHaveLength(3)
    }
    finally {
      vi.useRealTimers()
    }
  })

  it('says a message was taken back, and offers nothing on it', async () => {
    const wrapper = await mountSuspended(ChatBubble, {
      props: { message: message({ text: '', reactions: [], isDeleted: true }), now: Date.now() },
    })

    expect(wrapper.get('[data-testid="chat-deleted"]').text()).toBe('Nachricht gelöscht')
    expect(wrapper.find('[data-testid="chat-message-text"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="chat-kudos-Fire"]').exists()).toBe(false)
  })

  it('marks corrected words beside the time', async () => {
    const wrapper = await mountSuspended(ChatBubble, {
      props: { message: message({ editedAt: '2026-07-31T09:05:00Z' }), now: Date.now() },
    })

    expect(wrapper.get('[data-testid="chat-edited"]').text()).toBe('bearbeitet')
  })

  it('does not offer a reaction on your own message', async () => {
    const wrapper = await mountSuspended(ChatBubble, {
      props: { message: message({ isMine: true, senderName: null }), now: Date.now() },
    })
    expect(wrapper.find('[data-testid="chat-kudos-Fire"]').exists()).toBe(false)
  })

  it('trims a composed message, clears the field and asks for a photograph', async () => {
    const wrapper = await mountSuspended(ChatComposer)
    const input = wrapper.get('[data-testid="chat-input"]')

    await input.setValue('  Hallo  ')
    await wrapper.get('form').trigger('submit')
    expect(wrapper.emitted('send')?.[0]).toEqual(['Hallo'])
    expect((input.element as HTMLInputElement).value).toBe('')

    // No ready-made replies: what gets sent is what was typed.
    expect(wrapper.find('[data-testid="quick-cheer"]').exists()).toBe(false)

    const attach = wrapper.get('[data-testid="chat-attach"]')
    expect(attach.attributes('aria-label')).toBe('Foto senden')
    await attach.trigger('click')
    expect(wrapper.emitted('attach')).toHaveLength(1)
    expect(wrapper.emitted('send')).toHaveLength(1)
  })

  it('holds the photograph button back while a message is on its way', async () => {
    const wrapper = await mountSuspended(ChatComposer, { props: { busy: true } })

    expect(wrapper.get('[data-testid="chat-attach"]').attributes()).toHaveProperty('disabled')
  })

  it('draws a photograph at its own proportions, with or without words', async () => {
    const image = {
      id: 'image-1',
      purpose: 'ChatPhoto' as const,
      width: 1200,
      height: 900,
      byteSize: 1000,
      createdAt: '2026-07-31T09:00:00Z',
    }

    const alone = await mountSuspended(ChatBubble, {
      props: { message: message({ text: '', image }), now: Date.now() },
    })
    const frame = alone.get('[data-testid="chat-photo"]')
    const img = frame.get('img')

    expect(img.attributes('src')).toContain('/api/images/image-1')
    expect(img.attributes('crossorigin')).toBe('use-credentials')
    expect(img.attributes('alt')).toBe('Foto')
    expect(frame.attributes('style')).toContain('aspect-ratio: 1.3333')

    // Nothing but the picture: an empty bubble under it would be noise.
    expect(alone.find('p').exists()).toBe(false)

    // Somebody's photograph is blocked from Session Replay with the rest of
    // the message.
    expect(alone.attributes()).toHaveProperty('data-q2-block')

    const captioned = await mountSuspended(ChatBubble, {
      props: { message: message({ text: 'Gipfel!', image }), now: Date.now() },
    })
    expect(captioned.find('[data-testid="chat-photo"]').exists()).toBe(true)
    expect(captioned.get('p').text()).toBe('Gipfel!')

    // A panorama is not drawn as a sliver.
    const wide = await mountSuspended(ChatBubble, {
      props: { message: message({ text: '', image: { ...image, width: 2000, height: 400 } }), now: Date.now() },
    })
    expect(wide.get('[data-testid="chat-photo"]').attributes('style')).toContain('aspect-ratio: 1.5')
  })

  it('says a photograph is gone rather than drawing an empty bubble', async () => {
    const wrapper = await mountSuspended(ChatBubble, {
      props: { message: message({ text: '', image: null }), now: Date.now() },
    })

    expect(wrapper.find('[data-testid="chat-photo"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Foto nicht mehr verfügbar')
  })

  it('falls back to a placeholder when a photograph cannot be loaded', async () => {
    const image = {
      id: 'image-1',
      purpose: 'ChatPhoto' as const,
      width: 400,
      height: 400,
      byteSize: 1000,
      createdAt: '2026-07-31T09:00:00Z',
    }
    const wrapper = await mountSuspended(ChatBubble, {
      props: { message: message({ text: '', image }), now: Date.now() },
    })

    await wrapper.get('[data-testid="chat-photo"] img').trigger('error')

    expect(wrapper.find('[data-testid="chat-photo"] img').exists()).toBe(false)
    expect(wrapper.get('[data-testid="chat-photo"]').text()).toContain('Foto nicht mehr verfügbar')
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
    expect(wrapper.find('[data-testid="chat-banner-deliver"]').exists()).toBe(false)
  })

  /**
   * The owner is the one person in their goal's thread who can deliver, and
   * the banner used to offer them "Anfeuern" — a cheer from themselves (#88).
   */
  it('offers the owner a proof while one is due, and never a cheer', async () => {
    const mine = {
      id: 'goal-1',
      title: 'Halbmarathon',
      current: goal().current,
      streak: 2,
      isMine: true,
    } as ChatPinnedGoal

    const due = await mountSuspended(ChatGoalBanner, { props: { goal: mine, canDeliver: true } })
    expect(due.find('[data-testid="chat-cheer"]').exists()).toBe(false)
    await due.get('[data-testid="chat-banner-deliver"]').trigger('click')
    expect(due.emitted('deliver')).toHaveLength(1)

    // Nothing due (a vote is pending, or the window is full): nothing to press.
    const waiting = await mountSuspended(ChatGoalBanner, { props: { goal: mine, canDeliver: false } })
    expect(waiting.find('[data-testid="chat-cheer"]').exists()).toBe(false)
    expect(waiting.find('[data-testid="chat-banner-deliver"]').exists()).toBe(false)
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
      lastMessageIsDeleted: false,
      unreadCount: 2,
    } as ChatSummary
    const unread = await mountSuspended(ChatListRow, { props: { chat: summary, now: Date.now() } })
    expect(unread.text()).toContain('Jonas: Bis morgen')
    expect(unread.text()).toContain('2')

    const empty = await mountSuspended(ChatListRow, {
      props: { chat: { ...summary, lastMessage: null, lastMessageAt: null, unreadCount: 0 }, now: Date.now() },
    })
    expect(empty.text()).toContain('Noch keine Nachrichten')

    // A photograph without words is named as one, with who sent it.
    const photo = await mountSuspended(ChatListRow, {
      props: { chat: { ...summary, lastMessage: null, lastMessageHasPhoto: true }, now: Date.now() },
    })
    expect(photo.text()).toContain('Jonas: Foto')
  })
})

describe('feature cards and rows', () => {
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

  /**
   * Accepting lets somebody see your goals and photographs, so the person in
   * a request, a sent request and a suggestion opens their profile — and says
   * their handle, which nobody else can take (#87). The buttons stay apart.
   */
  it('opens the person behind a request, a sent request and a suggestion', async () => {
    const request = {
      person: person({ id: 'request-1', displayName: 'Emma', handle: '@emma.r' }),
      requestedAt: '2026-07-31T09:00:00Z',
      mutualFriends: 1,
    } as FriendRequest
    const requestRow = await mountSuspended(FriendRequestRow, { props: { request } })
    const requester = requestRow.get('[data-testid="request-person"]')
    expect(requester.attributes('href')).toBe('/people/request-1')
    expect(requester.text().replace(/\s+/g, ' ')).toContain('@emma.r · 1 gemeinsamer Freund')
    expect(requester.find('[data-testid="request-accept"]').exists()).toBe(false)

    const lonely = await mountSuspended(FriendRequestRow, { props: { request: { ...request, mutualFriends: 0 } } })
    expect(lonely.get('[data-testid="request-person"]').text()).not.toContain('gemeinsam')

    const sentRow = await mountSuspended(SentRequestRow, {
      props: { request: { person: person({ id: 'sent-1' }), requestedAt: '2026-07-31T09:00:00Z' } as SentRequest },
    })
    expect(sentRow.get('[data-testid="sent-request-person"]').attributes('href')).toBe('/people/sent-1')

    const suggestionRow = await mountSuspended(FriendSuggestionRow, {
      props: { suggestion: { person: person({ id: 'suggestion-1' }), mutualFriends: 3 } as FriendSuggestion },
    })
    const suggested = suggestionRow.get('[data-testid="suggestion-person"]')
    expect(suggested.attributes('href')).toBe('/people/suggestion-1')
    expect(suggested.find('[data-testid="suggestion-request"]').exists()).toBe(false)
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

    // Five destinations, and the middle one is the Dos — not a create
    // button, which is the plus on that screen now.
    expect(wrapper.findAll('a')).toHaveLength(5)
    expect(wrapper.get('[data-testid="nav-home"]').attributes('href')).toBe('/')
    expect(wrapper.get('[data-testid="nav-dos"]').attributes('href')).toBe('/goals')
    expect(wrapper.get('[data-testid="nav-dos"]').text()).toBe('Dos')
    expect(wrapper.find('[data-testid="nav-create"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="nav-chats"]').text()).toContain('3')

    // Friend requests followed the friends screen into search.
    expect(wrapper.get('[data-testid="nav-search"]').text()).toContain('2')
  })
})
