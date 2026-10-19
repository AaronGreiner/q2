import { mountSuspended } from '@nuxt/test-utils/runtime'
import { afterEach, describe, expect, it, vi } from 'vitest'
import ChatMessageActions from '~/components/chats/ChatMessageActions.vue'
import ChatEditSheet from '~/components/chats/ChatEditSheet.vue'
import type { ChatMessage } from '~/api/types'

afterEach(() => {
  vi.useRealTimers()
  document.body.replaceChildren()
})

function message(overrides: Partial<ChatMessage> = {}): ChatMessage {
  return {
    id: 'message-1',
    senderId: 'person-1',
    senderName: null,
    text: 'Bis morgn',
    image: null,
    sentAt: '2026-10-19T09:00:00Z',
    isMine: true,
    reactions: [],
    editedAt: null,
    isDeleted: false,
    canEdit: true,
    ...overrides,
  }
}

function actions(): string[] {
  return [...document.querySelectorAll('[data-testid^="chat-action-"]')].map(button => button.getAttribute('data-testid')!)
}

/**
 * A message's menu (#25, #104). What it offers follows the message as the
 * server describes it; what happens next is the screen's.
 */
describe('ChatMessageActions', () => {
  it('offers correcting and taking back your own, while correcting is still allowed', async () => {
    const own = await mountSuspended(ChatMessageActions, { props: { open: true, message: message() }, attachTo: document.body })
    expect(actions()).toEqual(['chat-action-edit', 'chat-action-delete'])
    own.unmount()

    const late = await mountSuspended(ChatMessageActions, { props: { open: true, message: message({ canEdit: false }) }, attachTo: document.body })
    expect(actions()).toEqual(['chat-action-delete'])
    late.unmount()
  })

  it('offers reporting somebody else\'s, and nothing on one taken back', async () => {
    const theirs = await mountSuspended(ChatMessageActions, { props: { open: true, message: message({ isMine: false, canEdit: false }) }, attachTo: document.body })
    expect(actions()).toEqual(['chat-action-report'])
    theirs.unmount()

    const gone = await mountSuspended(ChatMessageActions, { props: { open: true, message: message({ isDeleted: true, text: '' }) }, attachTo: document.body })
    expect(actions()).toEqual([])
    gone.unmount()
  })

  /** Handed on once the sheet is out of the way: two drawers at once deadlock. */
  it('closes first and hands the choice on afterwards', async () => {
    vi.useFakeTimers()
    const wrapper = await mountSuspended(ChatMessageActions, { props: { open: true, message: message() }, attachTo: document.body })

    document.querySelector<HTMLButtonElement>('[data-testid="chat-action-delete"]')!.click()
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
    expect(wrapper.emitted('delete')).toBeUndefined()

    vi.advanceTimersByTime(600)
    expect(wrapper.emitted('delete')?.[0]).toEqual([message()])
    wrapper.unmount()
  })
})

describe('ChatEditSheet', () => {
  it('starts from the words as they are and saves only a change', async () => {
    const wrapper = await mountSuspended(ChatEditSheet, { props: { open: true, message: message() }, attachTo: document.body })

    const field = document.querySelector<HTMLTextAreaElement>('[data-testid="chat-edit-text"] textarea, textarea[data-testid="chat-edit-text"]')!
    expect(field.value).toBe('Bis morgn')
    expect(document.querySelector<HTMLButtonElement>('[data-testid="chat-edit-save"]')!.disabled).toBe(true)

    field.value = 'Bis morgen'
    field.dispatchEvent(new Event('input'))
    await wrapper.vm.$nextTick()

    document.querySelector<HTMLButtonElement>('[data-testid="chat-edit-save"]')!.click()
    await wrapper.vm.$nextTick()
    expect(wrapper.emitted('save')?.[0]).toEqual(['Bis morgen'])
    wrapper.unmount()
  })
})
