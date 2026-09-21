import { mountSuspended } from '@nuxt/test-utils/runtime'
import { describe, expect, it } from 'vitest'
import ChatComposer from '~/components/chats/ChatComposer.vue'
import ChatEventLine from '~/components/chats/ChatEventLine.vue'
import ChatListRow from '~/components/chats/ChatListRow.vue'
import ProofCard from '~/components/proofs/ProofCard.vue'
import type { ChatSummary, GoalEvent, Proof } from '~/api/types'

/**
 * A goal's conversation: the lines between its messages, the row it has in the
 * list, the photograph in it and the camera that delivers one
 * (docs/adr/0027-goal-conversations.md).
 */
function event(overrides: Partial<GoalEvent> = {}): GoalEvent {
  return {
    key: 'Created-1',
    kind: 'Created',
    at: '2026-07-31T09:00:00+00:00',
    actorName: 'Jonas Weber',
    isMine: false,
    streak: null,
    confirmedProofs: null,
    requiredProofs: null,
    until: null,
    proof: null,
    ...overrides,
  }
}

function summary(overrides: Partial<ChatSummary> = {}): ChatSummary {
  return {
    id: 'chat-1',
    kind: 'Goal',
    name: 'Jeden Morgen joggen',
    initials: 'JM',
    icon: 'sunrise',
    avatarColor: '#0f766e',
    avatarImageId: null,
    isOnline: false,
    lastMessage: null,
    lastMessageSenderName: null,
    lastMessageIsMine: false,
    lastMessageAt: '2026-07-31T09:00:00+00:00',
    unreadCount: 0,
    isMuted: false,
    lastEvent: 'WindowDone',
    goalId: 'goal-1',
    isMyGoal: false,
    awaitingMyVote: false,
    ...overrides,
  }
}

function proof(overrides: Partial<Proof> = {}): Proof {
  return {
    id: 'proof-1',
    goalId: 'goal-1',
    goalInstanceId: 'window-1',
    uploader: {
      id: 'person-1',
      displayName: 'Jonas Weber',
      handle: '@jonas.w',
      initials: 'JW',
      avatarColor: '#4f46e5',
      isOnline: true,
      avatarImageId: null,
    },
    imageId: 'image-1',
    status: 'Confirmed',
    attempt: 1,
    attemptsLeft: 1,
    capturedInApp: true,
    createdAt: '2026-06-15T09:00:00+00:00',
    expiresAt: '2026-06-15T21:00:00+00:00',
    votes: { confirmCount: 1, doubtCount: 0, confirmedBy: [], myVote: 'Confirm', canIVote: false },
    reactions: [],
    ...overrides,
  }
}

describe('ChatEventLine', () => {
  it('names who did it, and says "Du" for the reader', async () => {
    const theirs = await mountSuspended(ChatEventLine, { props: { event: event() } })
    expect(theirs.text()).toBe('Jonas Weber hat das Ziel erstellt.')

    const mine = await mountSuspended(ChatEventLine, {
      props: { event: event({ isMine: true, actorName: null }) },
    })
    expect(mine.text()).toBe('Du hast das Ziel erstellt.')
  })

  it('keeps the line out of Session Replay, since it carries a name', async () => {
    const wrapper = await mountSuspended(ChatEventLine, { props: { event: event() } })

    expect(wrapper.find('[data-q2-private]').exists()).toBe(true)
  })

  /**
   * The flame means a streak and nothing else; a miss is stated, in grey, and
   * says how far it got rather than anything about the person.
   */
  it('draws a kept window with the streak and a missed one flat', async () => {
    const done = await mountSuspended(ChatEventLine, {
      props: { event: event({ kind: 'WindowDone', actorName: null, streak: 4 }) },
    })
    expect(done.text()).toBe('Geschafft · Streak 4')
    expect(done.find('.text-\\(--q2-flame-text\\)').exists()).toBe(true)

    const missed = await mountSuspended(ChatEventLine, {
      props: { event: event({ kind: 'WindowMissed', actorName: null, confirmedProofs: 1, requiredProofs: 3 }) },
    })
    expect(missed.text()).toBe('Verpasst · 1 von 3 geliefert')
    expect(missed.find('.text-\\(--q2-flame-text\\)').exists()).toBe(false)
  })

  it('says until when a pause runs, and never why', async () => {
    const wrapper = await mountSuspended(ChatEventLine, {
      props: { event: event({ kind: 'PauseStarted', until: '2026-08-03' }) },
    })

    expect(wrapper.text()).toBe('Jonas Weber setzt aus bis 3.8.')
  })
})

describe('ChatListRow for a goal', () => {
  it('shows what last happened to the goal when that was newer than any message', async () => {
    const wrapper = await mountSuspended(ChatListRow, { props: { chat: summary(), now: Date.now() } })

    expect(wrapper.text()).toContain('Jeden Morgen joggen')
    expect(wrapper.text()).toContain('Geschafft')
  })

  it('says a photograph is waiting for the reader, apart from being unread', async () => {
    const wrapper = await mountSuspended(ChatListRow, {
      props: { chat: summary({ awaitingMyVote: true, lastEvent: 'ProofDelivered' }), now: Date.now() },
    })

    expect(wrapper.get('[data-testid="chat-awaiting-vote"]').text()).toBe('Wartet auf dein Urteil')
  })
})

describe('ProofCard in a thread', () => {
  it('shows a decided photograph by its outcome, not by a deadline long gone', async () => {
    const wrapper = await mountSuspended(ProofCard, { props: { proof: proof() } })

    expect(wrapper.find('[data-testid="proof-expiry"]').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('Frist abgelaufen')
    expect(wrapper.get('[data-testid="proof-status"]').text()).toBe('Bestätigt')
  })
})

describe('ChatComposer in the owner\'s goal conversation', () => {
  it('offers the camera only when the page says the window takes a photograph', async () => {
    const without = await mountSuspended(ChatComposer)
    expect(without.find('[data-testid="chat-deliver"]').exists()).toBe(false)

    const withCamera = await mountSuspended(ChatComposer, { props: { canDeliver: true } })
    await withCamera.get('[data-testid="chat-deliver"]').trigger('click')

    expect(withCamera.emitted('deliver')).toHaveLength(1)
    expect(withCamera.get('[data-testid="chat-deliver"]').attributes('aria-label')).toBe('Beweis liefern')
  })
})
