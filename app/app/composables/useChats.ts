import type { ApiFailure } from '~/api/errors'
import type { ChatSummary, KudosKind } from '~/api/types'
import { isFirstLoad, placeholder } from '~/utils/firstLoad'

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
    { watch: [search], default: (): ChatsPayload => placeholder({ chats: [], failure: null }) },
  )

  return {
    chats: computed(() => data.value?.chats ?? []),
    error: computed(() => data.value?.failure ?? null),

    // Not while a reply that just arrived is being read in: see firstLoad.
    isLoading: computed(() => isFirstLoad(status.value, data.value)),
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
  const router = useRouter()
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
    { watch: [id], default: () => placeholder({ chat: null, failure: null }) },
  )

  const chat = computed(() => data.value?.chat ?? null)
  const failure = computed(() => data.value?.failure ?? null)
  const isMissing = computed(() => failure.value?.kind === 'notFound')
  const error = computed(() => (failure.value && failure.value.kind !== 'notFound' ? failure.value : null))

  // Only before the thread first arrives. A reply read in while somebody is
  // typing must not swap the thread — and the composer with their words in
  // it — for a skeleton: see firstLoad.
  const isLoading = computed(() => isFirstLoad(status.value, data.value))
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

  async function react(messageId: string, kind: KudosKind) {
    try {
      const updated = await api.chats.react(id.value, messageId, kind)
      data.value = { chat: updated, failure: null }
    }
    catch (caught) {
      report(caught, { feature: 'chats', action: 'react' })
    }
  }

  /**
   * Stops this conversation ringing on this person's devices, or lets it ring
   * again. It still counts as unread either way — muting is about being
   * interrupted, not about what is waiting when somebody looks.
   */
  async function setMuted(muted: boolean) {
    try {
      data.value = { chat: await api.chats.mute(id.value, muted), failure: null }
      toast.show(muted ? t.value.toast.chatMuted : t.value.toast.chatUnmuted)
    }
    catch (caught) {
      report(caught, { feature: 'chats', action: 'mute' })
    }
  }

  /**
   * Leaves a group and goes back to the list.
   *
   * The list is refreshed rather than filtered in place: this conversation is
   * gone from it, and so is the unread count the tab bar drew from it.
   */
  async function leave() {
    try {
      await api.chats.leave(id.value)
      await Promise.all([refreshNuxtData('chats'), refreshNuxtData('counts')])
      await router.push('/chats')
      toast.show(t.value.toast.groupLeft)
    }
    catch (caught) {
      report(caught, { feature: 'chats', action: 'leave' })
    }
  }

  return { chat, error, isMissing, isLoading, refresh, send, cheer, react, setMuted, leave, isSending }
}
