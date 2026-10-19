import type { ApiFailure } from '~/api/errors'
import type { ChatDetail, ChatMessage, ChatPhoto, ChatSummary, Image, KudosKind, ProofVoteValue } from '~/api/types'
import { carryOver, prependPage, withMessage, withProof } from '~/utils/chatPages'
import type { ThreadHistory } from '~/utils/chatPages'
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
 * One thread: the messages, the goal it belongs to, and writing into it.
 *
 * The thread arrives a page at a time (#44). What is read and written under its
 * key is the newest page; every write answers with that page rather than just
 * what changed, because a reply may have arrived while a message was being
 * typed, and re-rendering from one authoritative answer is simpler to get right
 * than merging two. What was read further back is the history, kept in front of
 * the newest page as it moves on (utils/chatPages.ts). A reaction and a vote
 * answer with the one line they changed, which is patched wherever it is.
 *
 * A goal's conversation is also a view of the goal — its photographs and what
 * became of its windows are read off it. So whatever makes the goal's own
 * screen read again makes this thread read again: a photograph arriving, a
 * verdict, a pause (`keysFor` names `goal:<id>` for all of them).
 */
export function useChatThread(id: Ref<string>) {
  const api = useQ2Api()
  const goBack = useGoBack()
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

  /*
   * Moved on with the newest page synchronously, so that no render ever sees
   * a new page beside a history that has not taken in the lines it pushed out.
   * Immediate, so a page that came with the server's render starts it too.
   */
  const history = shallowRef<ThreadHistory | null>(null)

  watch(chat, (next, previous) => {
    history.value = next ? carryOver(history.value, previous ?? null, next) : null
  }, { immediate: true, flush: 'sync' })

  const loaded = computed(() => (chat.value && history.value?.chatId === chat.value.id ? history.value : null))

  /** Everything loaded, oldest first: the history, then the newest page. */
  const messages = computed(() => [...(loaded.value?.messages ?? []), ...(chat.value?.messages ?? [])])
  const events = computed(() => [...(loaded.value?.events ?? []), ...(chat.value?.events ?? [])])
  const hasOlder = computed(() => Boolean(loaded.value?.older))

  const isLoadingOlder = ref(false)
  const olderFailed = ref(false)

  const failure = computed(() => data.value?.failure ?? null)
  const isMissing = computed(() => failure.value?.kind === 'notFound')
  const error = computed(() => (failure.value && failure.value.kind !== 'notFound' ? failure.value : null))

  // Only before the thread first arrives. A reply read in while somebody is
  // typing must not swap the thread — and the composer with their words in
  // it — for a skeleton: see firstLoad.
  const isLoading = computed(() => isFirstLoad(status.value, data.value))
  const isSending = ref(false)
  const isVoting = ref(false)

  if (import.meta.client) {
    const unhook = useNuxtApp().hook('app:data:refresh', async (keys) => {
      const goalId = data.value?.chat?.pinnedGoal?.id

      // With no keys everything is being read again already, this included.
      if (goalId && keys?.includes(`goal:${goalId}`)) await refresh()

      /*
       * A line that was edited or taken back (#25). The newest page is read
       * again through `chat:<id>` anyway; a line further back than that is
       * only in the history, and is fetched on its own rather than by reading
       * every page loaded again.
       */
      for (const key of keys ?? []) {
        if (!key.startsWith('chat-message:')) continue

        const messageId = key.slice('chat-message:'.length)
        if (history.value?.messages.some(message => message.id === messageId)) await refreshLine(messageId)
      }
    })

    onScopeDispose(unhook)
  }

  /** One line further back, read again. Quiet if it fails: the next page read corrects it. */
  async function refreshLine(messageId: string) {
    try {
      patch(await api.chats.message(id.value, messageId))
    }
    catch (caught) {
      report(caught, { feature: 'chats', action: 'line' })
    }
  }

  /** A message as the server now has it, put wherever it is loaded. */
  function patch(updated: ChatMessage) {
    const page = chat.value && withMessage(chat.value, updated)
    if (page && page !== chat.value) showPage(page)
    if (history.value) history.value = withMessage(history.value, updated)
  }

  /** Replaces the newest page, the shallow-ref way (see send). */
  function showPage(updated: ChatDetail) {
    data.value = { chat: updated, failure: null }
  }

  /**
   * Reads the page before the oldest line loaded. Returns whether one arrived,
   * so the screen can keep its place once it is drawn.
   *
   * A failure leaves what is loaded alone and is not tried again by itself:
   * the screen offers to, rather than asking again on every scroll.
   */
  async function loadOlder() {
    const before = loaded.value
    if (!before?.older || isLoadingOlder.value) return false

    isLoadingOlder.value = true
    olderFailed.value = false

    try {
      const page = await api.chats.older(id.value, before.older)
      const current = history.value

      // Another thread, or a history that started again meanwhile: this page
      // belongs in front of something that is no longer there.
      if (!current || current.chatId !== before.chatId || current.older !== before.older) return false

      history.value = prependPage(current, page)
      return true
    }
    catch (caught) {
      report(caught, { feature: 'chats', action: 'older' })
      olderFailed.value = true
      return false
    }
    finally {
      isLoadingOlder.value = false
    }
  }

  async function send(text: string) {
    const trimmed = text.trim()
    if (!trimmed || isSending.value) return false

    isSending.value = true
    try {
      const updated = await api.chats.send(id.value, { text: trimmed })

      // Replaced rather than mutated: `useAsyncData` returns a shallow ref, so
      // assigning to `data.value.chat` would send the message and leave the
      // thread on screen unchanged.
      showPage(updated)
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

  /**
   * A photograph, already uploaded as a `ChatPhoto`, sent as a message of its
   * own.
   *
   * If the message does not go through, the upload is deleted again: a picture
   * nobody was sent is still a picture on the server, counted against this
   * person's allowance and reachable by nobody but them.
   */
  async function sendPhoto(image: Image) {
    if (isSending.value) return false

    isSending.value = true
    try {
      showPage(await api.chats.send(id.value, { imageId: image.id }))
      return true
    }
    catch (caught) {
      report(caught, { feature: 'chats', action: 'send-photo' })
      await api.images.remove(image.id).catch(() => {
        // Best effort. It is still theirs and still in their allowance, which
        // is where a failed tidy-up leaves it anyway.
      })
      return false
    }
    finally {
      isSending.value = false
    }
  }

  /** The pinned goal's "Anfeuern" button — a message, not a separate concept. */
  async function cheer() {
    await send(t.value.chats.cheerText)
  }

  /** Patched wherever the message is: on the newest page, or pages back. */
  async function react(messageId: string, kind: KudosKind) {
    try {
      patch(await api.chats.react(id.value, messageId, kind))
    }
    catch (caught) {
      report(caught, { feature: 'chats', action: 'react' })
    }
  }

  const isChangingMessage = ref(false)

  /**
   * Corrects the words of your own message (#25). Answers with the failure, so
   * the sheet can say under the field why the server would not take it — the
   * fifteen minutes running out while it was open, say.
   */
  async function editMessage(messageId: string, text: string): Promise<ApiFailure | null> {
    if (isChangingMessage.value) return null

    isChangingMessage.value = true

    try {
      patch(await api.chats.edit(id.value, messageId, text))
      return null
    }
    catch (caught) {
      return report(caught, { feature: 'chats', action: 'edit' })
    }
    finally {
      isChangingMessage.value = false
    }
  }

  /** Takes back your own message for everybody (#25). Its place stays, saying so. */
  async function deleteMessage(messageId: string) {
    if (isChangingMessage.value) return

    isChangingMessage.value = true

    try {
      patch(await api.chats.remove(id.value, messageId))
      toast.show(t.value.toast.messageDeleted)
    }
    catch (caught) {
      report(caught, { feature: 'chats', action: 'delete' })
    }
    finally {
      isChangingMessage.value = false
    }
  }

  /**
   * Pins the conversation to the top of its section of this person's list, or
   * unpins it (#24). Answers with the failure, so the sheet can say that three
   * are already pinned.
   */
  async function setPinned(pinned: boolean): Promise<ApiFailure | null> {
    try {
      showPage(await api.chats.pin(id.value, pinned))
      await refreshNuxtData('chats')
      return null
    }
    catch (caught) {
      return report(caught, { feature: 'chats', action: 'pin' })
    }
  }

  /**
   * A verdict on a photograph in the thread — the same vote as on the vote
   * screen, through the same endpoint and the same rules.
   *
   * The newest page is read again rather than patched: a vote can decide the
   * photograph, keep the window and move the streak, and the server is the
   * one that knows which. A photograph pages back is not on it, so the answer
   * to the vote itself is put where that one is. The vote screen's queue and
   * the badges move too.
   */
  async function vote(proofId: string, value: ProofVoteValue) {
    if (isVoting.value) return

    isVoting.value = true

    try {
      const proof = await api.proofs.vote(proofId, value)
      if (history.value) history.value = withProof(history.value, proof)
    }
    catch (caught) {
      // Out of step — the vote closed, or it was already cast elsewhere.
      // Reading the thread again is the honest answer either way.
      report(caught, { feature: 'chats', action: 'vote' })
    }
    finally {
      await Promise.all([refresh(), refreshNuxtData(['counts', 'proofs-pending'])])
      isVoting.value = false
    }
  }

  /**
   * Stops this conversation ringing on this person's devices, or lets it ring
   * again. It still counts as unread either way — muting is about being
   * interrupted, not about what is waiting when somebody looks.
   */
  async function setMuted(muted: boolean) {
    try {
      showPage(await api.chats.mute(id.value, muted))
    }
    catch (caught) {
      report(caught, { feature: 'chats', action: 'mute' })
    }
  }

  /**
   * Leaves a group and goes back to where the thread was opened from — the
   * list, usually — rather than pushing the list on top of a conversation that
   * no longer exists for this person (#118).
   *
   * The list is refreshed rather than filtered in place: this conversation is
   * gone from it, and so is the unread count the tab bar drew from it.
   */
  async function leave() {
    try {
      await api.chats.leave(id.value)
      await Promise.all([refreshNuxtData('chats'), refreshNuxtData('counts')])
      goBack('/chats')
      toast.show(t.value.toast.groupLeft)
    }
    catch (caught) {
      report(caught, { feature: 'chats', action: 'leave' })
    }
  }

  return {
    chat,
    messages,
    events,
    hasOlder,
    isLoadingOlder: computed(() => isLoadingOlder.value),
    olderFailed: computed(() => olderFailed.value),
    loadOlder,
    error,
    isMissing,
    isLoading,
    refresh,
    send,
    sendPhoto,
    cheer,
    react,
    vote,
    setMuted,
    setPinned,
    editMessage,
    deleteMessage,
    isChangingMessage: computed(() => isChangingMessage.value),
    leave,
    isSending,
    isVoting: computed(() => isVoting.value),
  }
}

/**
 * A conversation's photographs, for its info sheet (#24).
 *
 * Read when the sheet opens rather than with the thread: most visits never
 * open it, and the grid is its own read on the server for the same reason —
 * a thread is paged (ADR 0043), and paging through it for pictures would be
 * the slow way to fill a grid.
 */
export function useChatPhotos(id: Ref<string>) {
  const api = useQ2Api()
  const { report } = useErrorReporter()

  const photos = shallowRef<ChatPhoto[] | null>(null)
  const failure = ref<ApiFailure | null>(null)
  const isLoading = ref(false)

  async function load() {
    if (isLoading.value) return

    isLoading.value = true
    failure.value = null

    try {
      photos.value = await api.chats.photos(id.value)
    }
    catch (caught) {
      failure.value = report(caught, { feature: 'chats', action: 'photos' })
    }
    finally {
      isLoading.value = false
    }
  }

  return {
    photos: computed(() => photos.value),
    failure: computed(() => failure.value),
    isLoading: computed(() => isLoading.value),
    load,
  }
}
