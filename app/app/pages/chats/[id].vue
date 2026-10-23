<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'
import type { ChatMessage, Image, ReportReason } from '~/api/types'
import type { ThreadItem } from '~/utils/chatTimeline'
import type { Captured } from '~/utils/captured'

/**
 * One conversation.
 *
 * A goal's conversation is where its friends check it: its photographs arrive
 * here with their vote, and what became of each window is a line between the
 * messages (docs/adr/0027-goal-conversations.md). The goal's owner gets the
 * camera in the composer while the open window takes a photograph.
 *
 * Anybody can send a photograph into any conversation. It is uploaded as a
 * `ChatPhoto`, which the server shows to the people in this conversation and
 * nobody else, and it is never a proof: that is the camera above, and only it.
 *
 * Opening it marks it read on the server, which is why the navigation badge is
 * refreshed afterwards — the count has just changed under it. A reply arriving
 * while it is open comes in over the live connection, which refreshes this
 * thread by its key (useLiveConnection).
 *
 * It opens on its newest page. Scrolling towards the top reads the page before,
 * and the screen keeps its place while that page is put above what is being
 * read (#44).
 *
 * The bell in the header mutes this conversation on every device of this
 * person's. It is the one switch that is about a single conversation rather
 * than a kind of notification, so it lives here and not in the settings.
 */
// The header and the composer sit on the edges of the display and pad
// themselves — see app/layouts/plain.vue.
definePageMeta({ layout: 'plain', edgeToEdge: true })

/*
 * Installed on iOS the status bar has no colour of its own: the system tints it
 * with the page's background and picks a contrasting clock to sit on it. Every
 * other screen has --ui-bg at the top and gets that for free; here the top of
 * the screen is the header, so without this the bar above it comes out --ui-bg
 * and reads as a seam across the top of the conversation. See app/app.vue for
 * why the status bar is the system's to paint at all.
 */
useHead({ bodyAttrs: { class: 'q2-body-surface' } })

const route = useRoute()
const t = useMessages()
const now = useNow()
const zone = useTimeZoneOffset()
const { person: me } = useSession()

const id = computed(() => String(route.params.id))
const {
  chat,
  messages,
  events,
  hasOlder,
  isLoadingOlder,
  olderFailed,
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
  isChangingMessage,
  leave,
  isSending,
  isVoting,
} = useChatThread(id)

/*
 * One message's menu, and what it leads to (#25, #104): correcting or taking
 * back your own, reporting somebody else's. One of each for the whole screen,
 * like the camera, rather than one per bubble — and each opens only once the
 * menu has closed, because two drawers open at once deadlock (PhotoCapture).
 */
const actionsFor = ref<ChatMessage | null>(null)
const isActionsOpen = ref(false)

function openActions(message: ChatMessage) {
  if (message.isDeleted) return

  actionsFor.value = message
  isActionsOpen.value = true
}

const editing = ref<ChatMessage | null>(null)
const isEditOpen = ref(false)
const editFailure = ref<ApiFailure | null>(null)

function startEditing(message: ChatMessage) {
  editing.value = message
  editFailure.value = null
  isEditOpen.value = true
}

async function saveEdit(text: string) {
  if (!editing.value) return

  editFailure.value = await editMessage(editing.value.id, text)
  if (!editFailure.value) isEditOpen.value = false
}

const deleting = ref<ChatMessage | null>(null)
const isDeleteOpen = ref(false)

function askToDelete(message: ChatMessage) {
  deleting.value = message
  isDeleteOpen.value = true
}

async function confirmDelete() {
  if (deleting.value) await deleteMessage(deleting.value.id)
}

const safety = useSafety()
const reporting = ref<ChatMessage | null>(null)
const isReportOpen = ref(false)

function startReporting(message: ChatMessage) {
  reporting.value = message
  isReportOpen.value = true
}

async function onReport(reason: ReportReason, note: string) {
  if (reporting.value && await safety.report('Message', reporting.value.id, reason, note)) isReportOpen.value = false
}

/*
 * The info sheet's photographs and pin (#24). The photographs are read when
 * the sheet opens rather than with the thread: most visits never open it.
 */
const chatPhotos = useChatPhotos(id)
const pinFailure = ref<ApiFailure | null>(null)

async function onPin(pinned: boolean) {
  pinFailure.value = await setPinned(pinned)
}

/** Messages and what happened to the goal, as one thread by time — as far back as is loaded. */
const items = computed(() => (chat.value ? threadItems(messages.value, events.value) : []))

/** The same, with a separator per day and a run of misses folded into one line. */
const rows = computed(() => threadRows(items.value, now.value, t.value, zone.value))

/**
 * The camera, for the owner, while the open window takes a photograph —
 * `acceptsProof` rather than a rule of our own, because the server's answer
 * involves the attempt count and a pending vote as well.
 */
const { isDelivering, deliver, maxEdge } = useProofDelivery()
const isCameraOpen = ref(false)

const canDeliver = computed(() =>
  Boolean(chat.value?.pinnedGoal?.isMine && chat.value.pinnedGoal.current?.acceptsProof) && !isDelivering.value)

/** A Don't's use, logged from its conversation; the line it makes arrives in the thread. */
const dontUse = useDontUse(() => refresh())

/**
 * An objection to an edit, raised from the line it was made on — the same
 * request the goal's banner sends (docs/adr/0044-editing-a-running-goal.md).
 */
const lifecycle = useGoalLifecycle()

async function onChangeVeto() {
  const goalId = chat.value?.pinnedGoal?.id
  if (goalId && await lifecycle.toggleChangeVeto(goalId)) await refresh()
}

async function onLog() {
  const goalId = chat.value?.pinnedGoal?.id
  if (goalId && await dontUse.logUse(goalId)) await refresh()
}

/**
 * Already where the photograph is shown, so a delivery stays here and the card
 * arriving in the thread is the confirmation; a refusal stays on the camera's
 * screen with the photograph (see useProofDelivery).
 */
async function handIn(captured: Captured) {
  const goalId = chat.value?.pinnedGoal?.id
  if (!goalId) return null

  const result = await deliver(goalId, captured)
  if (result.status !== 'moved') await refresh()

  return result.status === 'refused' ? result.message : null
}

/** A photograph as a message, picked or taken in the same sheet as a proof. */
const isPhotoOpen = ref(false)

async function onPhotoPicked(image: Image) {
  isPhotoOpen.value = false
  await sendPhoto(image)
}

const thread = useTemplateRef<HTMLElement>('thread')

/** A thread is read from the bottom, so that is where it opens and stays. */
function scrollToLatest() {
  if (thread.value) thread.value.scrollTop = thread.value.scrollHeight
}

/**
 * Whether the reader is at the newest line. Read off the last scroll rather
 * than measured when something arrives, because by then the thread has already
 * grown under them.
 */
let atLatest = true

/** How close to the top the page before is asked for: about a screen, so it is there before the reader is. */
const olderMargin = 800

/** Something the reader did themselves, which the thread scrolls down to wherever they were. */
function isOwn(item: ThreadItem | undefined) {
  if (!item) return false
  return item.kind === 'message' ? item.message.isMine : item.event.isMine
}

/*
 * Every moment that puts the newest line out of sight, in one place.
 *
 * `thread` is the scroll region, and it only exists once the conversation has
 * loaded — which, on a tap from the list, is *after* this page is mounted.
 * Scrolling in `onMounted` therefore ran against nothing and left the
 * conversation sitting on its oldest message; watching the element itself
 * catches the render it appears in, whether that is the first one (the payload
 * came from the server) or a later one.
 *
 * After that it is the newest line that matters, not the number of lines: a
 * page read further back adds lines at the top and must not move the reader,
 * and a reaction adds none. A new line is followed when the reader was at the
 * bottom already, or when it is theirs — sending, the pinned goal's
 * "Anfeuern", a photograph — and otherwise left for them to scroll to, rather
 * than taking them out of last week's messages.
 *
 * `flush: 'post'` because the height being measured is the one after Vue has
 * patched the DOM, not before.
 */
watch([thread, () => items.value.at(-1)?.key], ([element], [previous]) => {
  if (!element) return
  if (element !== previous || atLatest || isOwn(items.value.at(-1))) scrollToLatest()
  void loadOlderNearTop()
}, { flush: 'post' })

/**
 * Reads the page before when the top is near, and keeps the reader's place.
 *
 * The lines arrive above what is on screen, which pushes it down by their
 * height; measuring from the bottom before and setting it back after keeps the
 * same line under the reader's thumb. The browser's own scroll anchoring is
 * off on the region (`overflow-anchor`), because where it exists it would make
 * the same correction a second time.
 *
 * A page that still leaves the top in sight — a run of short lines — asks for
 * the next one straight away, until the region can scroll or the thread
 * begins.
 */
async function loadOlderNearTop() {
  const element = thread.value
  if (!element || !hasOlder.value || isLoadingOlder.value || olderFailed.value) return
  if (element.scrollTop > olderMargin) return

  if (await loadOlderKeepingPlace()) await loadOlderNearTop()
}

/** The page before, put above the reader without moving what they are reading. */
async function loadOlderKeepingPlace() {
  const element = thread.value
  if (!element) return false

  const fromBottom = element.scrollHeight - element.scrollTop
  if (!await loadOlder()) return false

  await nextTick()
  element.scrollTop = element.scrollHeight - fromBottom
  return true
}

/*
 * Arriving for one photograph (#112) — from a reaction to it or a verdict on
 * it, by way of `middleware/goal-photo.ts`.
 *
 * Runs once the thread has drawn, after the scroll to the newest line above,
 * and takes over from it: the photograph is brought to the middle and outlined
 * for a moment — in grey, because being pointed at is not something to do. A
 * photograph further back is paged towards, a page at a time, as far as
 * `focusPages` goes; past that, or once the thread has begun, it opens full
 * screen instead, so the tap always ends on the picture it was about.
 *
 * `?photo=` is taken off the address afterwards, so going back to this
 * conversation later does not point at the photograph again.
 */
const focusPages = 10
const focusedPhoto = ref<string | null>(null)
const proofViewer = useProofViewer()
let focusState: 'idle' | 'busy' | 'done' = 'idle'

async function focusOnPhoto() {
  const photo = typeof route.query.photo === 'string' ? route.query.photo : null
  const element = thread.value
  if (!photo || !element || focusState !== 'idle' || !chat.value) return

  focusState = 'busy'
  const title = chat.value.pinnedGoal?.title ?? chat.value.name

  for (let page = 0; ; page++) {
    // The thread may be reading a page of its own as the reader nears the
    // top; this waits for it rather than racing it.
    await olderSettled()

    const card = element.querySelector<HTMLElement>(`[data-proof-id="${CSS.escape(photo)}"]`)

    if (card) {
      card.scrollIntoView({ block: 'center' })
      atLatest = false
      focusedPhoto.value = photo
      setTimeout(() => {
        if (focusedPhoto.value === photo) focusedPhoto.value = null
      }, 2400)
      break
    }

    if (page >= focusPages || !hasOlder.value || olderFailed.value) {
      await proofViewer.show(photo, title)
      break
    }

    await loadOlderKeepingPlace()
  }

  focusState = 'done'
  const { photo: _, ...query } = route.query
  await navigateTo({ path: route.path, query }, { replace: true })
}

/** Resolves once no page before is being read. */
async function olderSettled() {
  if (isLoadingOlder.value) {
    await new Promise<void>((resolve) => {
      const stop = watch(isLoadingOlder, (loading) => {
        if (loading) return
        stop()
        resolve()
      })
    })
  }

  await nextTick()
}

watch([thread, () => items.value.length], () => void focusOnPhoto(), { flush: 'post' })

function onScroll() {
  const element = thread.value
  if (!element) return

  atLatest = element.scrollHeight - element.scrollTop - element.clientHeight < 48
  void loadOlderNearTop()
}

// Somebody who hides their own activity sees nobody's (#106).
const seesActivity = useSeesActivity()

const status = computed(() => {
  if (!chat.value) return ''
  if (chat.value.kind !== 'Direct') return t.value.chats.members(chat.value.memberCount)
  if (!seesActivity.value) return ''
  if (chat.value.isOnline) return t.value.chats.online
  if (chat.value.otherLastSeenAt) {
    return t.value.chats.lastSeen(formatRelativeTime(chat.value.otherLastSeenAt, now.value, t.value))
  }
  return t.value.chats.offline
})

/**
 * Leaving is not undoable from inside the app — somebody in the group would
 * have to start a new one — so it asks first.
 */
const isLeaveOpen = ref(false)

/**
 * Who is in it, and the way to its goal — behind a tap on the header.
 *
 * Open again when the thread is gone back to from a member or the goal it led
 * to (#118): that is where the person was, and the sheet is what they were
 * reading.
 */
const isInfoOpen = ref(false)
isInfoOpen.value = useReturnState('chatInfo', () => isInfoOpen.value || undefined) ?? false

// Read each time it opens, so a photograph sent meanwhile is in it.
watch(isInfoOpen, (isOpen) => {
  if (isOpen) void chatPhotos.load()
  else pinFailure.value = null
}, { immediate: true })

function askToLeave() {
  isInfoOpen.value = false
  isLeaveOpen.value = true
}

onMounted(async () => {
  // The badge in the tab bar counts unread conversations, and this one is not
  // one any more.
  await refreshNuxtData('counts')
})

useHead({ title: () => chat.value?.name ?? t.value.chats.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <header
      v-if="chat"
      class="flex shrink-0 items-center gap-2.5 border-b border-(--ui-border) bg-(--q2-surface) px-[18px] py-2 pt-[calc(0.5rem+env(safe-area-inset-top))]"
    >
      <!-- Back to wherever the thread was opened from — a goal, a profile, the
           Friends tab — and to the list only when there is nothing before it. -->
      <AppBackLink to="/chats" />

      <!-- The name is the way in to who is here and what it is about. The
           avatar does not open full screen from here: a second target inside
           this one would be two things under one thumb. -->
      <h1 class="min-w-0 flex-1">
        <button
          type="button"
          class="flex min-h-11 w-full min-w-0 items-center gap-2.5 rounded-(--q2-radius-md) text-start focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
          aria-haspopup="dialog"
          data-testid="chat-header-info"
          @click="isInfoOpen = true"
        >
          <AppAvatar
            :initials="chat.initials"
            :color="chat.avatarColor"
            :image-id="chat.avatarImageId"
            :icon="chat.icon"
            :size="38"
            :online="chat.isOnline"
          />

          <span
            class="min-w-0 flex-1"
            data-q2-private
          >
            <span class="block truncate text-[15px] font-extrabold">{{ chat.name }}</span>
            <span
              v-if="status"
              class="block truncate text-[11px] font-bold text-(--ui-text-muted)"
            >{{ status }}</span>
            <span class="sr-only">{{ t.chats.openInfo }}</span>
          </span>
        </button>
      </h1>

      <!-- Grey in both states: muting is a state, not something waiting to be
           done. The last button in the row pulls into the edge padding. -->
      <button
        type="button"
        class="-me-2 flex size-11 shrink-0 items-center justify-center rounded-full text-(--ui-text-muted) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
        :aria-label="chat.isMuted ? t.chats.unmute : t.chats.mute"
        :aria-pressed="chat.isMuted"
        data-testid="mute-chat"
        @click="setMuted(!chat.isMuted)"
      >
        <UIcon
          :name="chat.isMuted ? 'i-lucide-bell-off' : 'i-lucide-bell'"
          class="size-5"
          aria-hidden="true"
        />
      </button>
    </header>

    <div
      v-if="isLoading"
      class="flex-1 p-4"
      aria-busy="true"
      aria-live="polite"
    >
      <span class="sr-only">{{ t.common.loading }}</span>
      <USkeleton class="h-14 w-2/3 rounded-(--q2-radius-lg)" />
      <USkeleton class="mt-3 ms-auto h-14 w-1/2 rounded-(--q2-radius-lg)" />
    </div>

    <div
      v-else-if="error || isMissing || !chat"
      class="flex-1 p-[18px]"
    >
      <AppErrorState
        v-if="error"
        :error="error"
        retryable
        @retry="refresh()"
      />

      <AppStateMessage
        v-else
        icon="i-lucide-message-circle"
        :title="t.chats.notFound"
        :description="t.chats.notFoundHint"
        data-testid="chat-not-found"
      >
        <UButton
          to="/chats"
          icon="i-lucide-chevron-left"
        >
          {{ t.chats.heading }}
        </UButton>
      </AppStateMessage>
    </div>

    <template v-else>
      <div
        ref="thread"
        class="q2-scroll flex flex-1 flex-col gap-2 bg-(--q2-surface) px-[18px] py-3.5 [overflow-anchor:none]"
        data-testid="chat-thread"
        @scroll.passive="onScroll"
      >
        <ChatGoalBanner
          v-if="chat.pinnedGoal"
          :goal="chat.pinnedGoal"
          :logging="dontUse.isLogging.value"
          :can-deliver="canDeliver"
          @log="onLog"
          @cheer="cheer()"
          @deliver="isCameraOpen = true"
        />

        <!-- The page before, on its way in. A failed one waits to be asked
             for again rather than retrying on every scroll. -->
        <div
          v-if="hasOlder"
          class="flex min-h-11 items-center justify-center"
          data-testid="chat-older"
        >
          <UButton
            v-if="olderFailed && !isLoadingOlder"
            variant="ghost"
            color="neutral"
            icon="i-lucide-rotate-ccw"
            data-testid="chat-older-retry"
            @click="loadOlderKeepingPlace"
          >
            {{ t.chats.olderRetry }}
          </UButton>

          <!-- The row keeps its height while idle, so the spinner appearing
               does not push the thread down under the reader. -->
          <span
            v-else
            role="status"
            :aria-busy="isLoadingOlder"
            class="text-(--ui-text-dimmed)"
          >
            <template v-if="isLoadingOlder">
              <UIcon
                name="i-lucide-loader-circle"
                class="size-5 animate-spin"
                aria-hidden="true"
              />
              <span class="sr-only">{{ t.chats.olderLoading }}</span>
            </template>
          </span>
        </div>

        <!-- Always rendered, so the first message animates in rather than
             mounting the whole list; each timeline item carries its own key. -->
        <TransitionGroup
          tag="ul"
          name="q2-message"
          class="flex list-none flex-col gap-1 p-0"
          data-testid="chat-messages"
        >
          <template
            v-for="item in rows"
            :key="item.key"
          >
            <li
              v-if="item.kind === 'day'"
              class="my-2 flex list-none justify-center"
              data-testid="chat-day"
            >
              <span class="text-[11px] font-bold tracking-wide text-(--ui-text-dimmed) uppercase">{{ item.label }}</span>
            </li>

            <ChatMissedRun
              v-else-if="item.kind === 'missedRun'"
              :from="item.from"
              :to="item.to"
              :count="item.count"
            />

            <ChatKeptRun
              v-else-if="item.kind === 'keptRun'"
              :from="item.from"
              :to="item.to"
              :count="item.count"
              :streak="item.streak"
              :held="item.held"
            />

            <ChatBubble
              v-else-if="item.kind === 'message'"
              :message="item.message"
              :now="now"
              @react="react"
              @actions="openActions"
            />

            <ChatEventLine
              v-else-if="item.kind === 'event'"
              :event="item.event"
              :busy="lifecycle.isBusy.value"
              @veto="onChangeVeto"
            />

            <!-- The owner's photograph sits on their side of the thread, a
                 friend's on the other, like the messages around it. -->
            <li
              v-else
              class="my-1.5 w-[85%] list-none rounded-(--q2-radius-lg) outline-offset-4 transition-[outline-color] duration-(--q2-motion-normal)"
              :class="[
                item.event.isMine ? 'self-end' : 'self-start',
                focusedPhoto === item.proof.id ? 'outline-2 outline-(--ui-border-accented)' : 'outline-transparent',
              ]"
              :data-proof-id="item.proof.id"
              :data-focused="focusedPhoto === item.proof.id ? '' : undefined"
              data-testid="chat-proof"
            >
              <ProofCard
                :proof="item.proof"
                :context="chat.pinnedGoal?.title ?? chat.name"
                :busy="isVoting"
                @vote="(value, reason) => vote(item.proof.id, value, reason)"
              />
            </li>
          </template>
        </TransitionGroup>

        <AppStateMessage
          v-if="items.length === 0"
          class="my-auto"
          icon="i-lucide-message-circle"
          :title="t.chats.empty"
          :description="t.chats.emptyHint"
        />
      </div>

      <ChatComposer
        :busy="isSending"
        :can-deliver="canDeliver"
        @send="send"
        @deliver="isCameraOpen = true"
        @attach="isPhotoOpen = true"
      />
    </template>

    <PhotoCapture
      v-model:open="isCameraOpen"
      purpose="Proof"
      proof-modes
      labels-source
      :max-edge="maxEdge"
      :hand-in="handIn"
    />

    <PhotoCapture
      v-model:open="isPhotoOpen"
      purpose="ChatPhoto"
      @uploaded="onPhotoPicked"
    />

    <ChatInfoSheet
      v-if="chat"
      v-model:open="isInfoOpen"
      :chat="chat"
      :me-id="me?.id ?? null"
      :photos="chatPhotos.photos.value"
      :photos-failed="chatPhotos.failure.value !== null"
      :pin-failure="pinFailure"
      @leave="askToLeave"
      @pin="onPin"
    />

    <ChatMessageActions
      v-model:open="isActionsOpen"
      :message="actionsFor"
      @edit="startEditing"
      @delete="askToDelete"
      @report="startReporting"
    />

    <ChatEditSheet
      v-model:open="isEditOpen"
      :message="editing"
      :busy="isChangingMessage"
      :failure="editFailure"
      @save="saveEdit"
    />

    <AppConfirmDialog
      v-model:open="isDeleteOpen"
      :title="t.chats.deleteHeading"
      :description="t.chats.deleteBody"
      :confirm-label="t.chats.deleteConfirm"
      @confirm="confirmDelete"
    />

    <ReportSheet
      v-if="reporting"
      v-model:open="isReportOpen"
      target-kind="Message"
      :target-id="reporting.id"
      :busy="safety.isBusy.value"
      @report="onReport"
    />

    <AppConfirmDialog
      v-model:open="isLeaveOpen"
      :title="t.chats.leaveGroup"
      :description="t.chats.leaveGroupConfirm"
      :confirm-label="t.chats.leaveGroup"
      @confirm="leave"
    />
  </div>
</template>
