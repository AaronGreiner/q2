import type { ApiFailure } from '~/api/errors'
import type { ChatSummary } from '~/api/types'

interface ChatsPayload {
  chats: ChatSummary[]
  failure: ApiFailure | null
}

/**
 * The list of conversations, with a search box over it.
 *
 * Filtering happens on the server so it matches what the server considers a
 * conversation's name — for a direct chat that is the other person, which the
 * client does not otherwise have to know.
 */
export function useChats(search: Ref<string>) {
  const api = useQ2Api()
  const { report } = useErrorReporter()

  const { data, status, refresh } = useAsyncData<ChatsPayload>(
    'chats',
    async () => {
      try {
        return { chats: await api.chats.list({ search: search.value }), failure: null }
      }
      catch (caught) {
        return { chats: [], failure: report(caught, { feature: 'chats', action: 'list' }) }
      }
    },
    { watch: [search], default: (): ChatsPayload => ({ chats: [], failure: null }) },
  )

  return {
    chats: computed(() => data.value?.chats ?? []),
    error: computed(() => data.value?.failure ?? null),
    isLoading: computed(() => status.value === 'pending'),
    refresh,
  }
}

/**
 * One thread: the messages, the pinned goal, and writing into it.
 *
 * Every write returns the whole thread rather than just what changed. A reply
 * may have arrived while a message was being typed, and re-rendering from one
 * authoritative answer is simpler to get right than merging two.
 */
export function useChatThread(id: Ref<string>) {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  const { data, status, refresh } = useAsyncData(
    () => `chat:${id.value}`,
    async () => {
      try {
        return { chat: await api.chats.get(id.value), failure: null }
      }
      catch (caught) {
        return { chat: null, failure: report(caught, { feature: 'chats', action: 'thread' }) }
      }
    },
    { watch: [id], default: () => ({ chat: null, failure: null }) },
  )

  const chat = computed(() => data.value?.chat ?? null)
  const failure = computed(() => data.value?.failure ?? null)
  const isMissing = computed(() => failure.value?.kind === 'notFound')
  const error = computed(() => (failure.value && failure.value.kind !== 'notFound' ? failure.value : null))
  const isLoading = computed(() => status.value === 'pending')
  const isSending = ref(false)

  async function send(text: string) {
    const trimmed = text.trim()
    if (!trimmed || isSending.value) return false

    isSending.value = true
    try {
      const updated = await api.chats.send(id.value, trimmed)

      // Replaced rather than mutated: `useAsyncData` returns a shallow ref, so
      // assigning to `data.value.chat` would send the message and leave the
      // thread on screen unchanged.
      data.value = { chat: updated, failure: null }
      return true
    }
    catch (caught) {
      report(caught, { feature: 'chats', action: 'send' })
      return false
    }
    finally {
      isSending.value = false
    }
  }

  /** The pinned goal's "Anfeuern" button — a message, not a separate concept. */
  async function cheer() {
    if (await send(t.value.chats.cheerText)) {
      toast.show(t.value.toast.cheerSent)
    }
  }

  async function react(messageId: string, emoji: string) {
    try {
      const updated = await api.chats.react(id.value, messageId, emoji)
      data.value = { chat: updated, failure: null }
    }
    catch (caught) {
      report(caught, { feature: 'chats', action: 'react' })
    }
  }

  return { chat, error, isMissing, isLoading, refresh, send, cheer, react, isSending }
}
