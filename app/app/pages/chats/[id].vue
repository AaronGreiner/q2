<script setup lang="ts">
import type { Image } from '~/api/types'

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

const id = computed(() => String(route.params.id))
const {
  chat,
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
  leave,
  isSending,
  isVoting,
} = useChatThread(id)

/** Messages and what happened to the goal, as one thread by time. */
const items = computed(() => (chat.value ? threadItems(chat.value.messages, chat.value.events) : []))

/**
 * The camera, for the owner, while the open window takes a photograph —
 * `acceptsProof` rather than a rule of our own, because the server's answer
 * involves the attempt count and a pending vote as well.
 */
const { isDelivering, deliver, maxEdge } = useProofDelivery()
const isCameraOpen = ref(false)

const canDeliver = computed(() =>
  Boolean(chat.value?.pinnedGoal?.isMine && chat.value.pinnedGoal.current?.acceptsProof) && !isDelivering.value)

/**
 * Already where the photograph is shown, so a delivery stays here and the card
 * arriving in the thread is the confirmation; a refusal stays on the camera's
 * screen with the photograph (see useProofDelivery).
 */
async function handIn(image: Image) {
  const goalId = chat.value?.pinnedGoal?.id
  if (!goalId) return null

  const result = await deliver(goalId, image)
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

/*
 * Every moment that puts the newest message out of sight, in one place.
 *
 * `thread` is the scroll region, and it only exists once the conversation has
 * loaded — which, on a tap from the list, is *after* this page is mounted.
 * Scrolling in `onMounted` therefore ran against nothing and left the
 * conversation sitting on its oldest message; watching the element itself
 * catches the render it appears in, whether that is the first one (the payload
 * came from the server) or a later one.
 *
 * The message count covers everything written from here — sending, and the
 * pinned goal's "Anfeuern", which is a message like any other. A reaction
 * leaves the count alone and does not move the thread under the reader.
 *
 * `flush: 'post'` because the height being measured is the one after Vue has
 * patched the DOM, not before.
 */
watch([thread, () => items.value.length], scrollToLatest, { flush: 'post' })

const status = computed(() => {
  if (!chat.value) return ''
  if (chat.value.kind !== 'Direct') return t.value.chats.members(chat.value.memberCount)
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
      <!-- `-ms-2` for the same reason as in AppScreenHeader: the arrow's box is
           bigger than the arrow, and lining up the box would leave the glyph
           looking indented against every other screen's title. -->
      <NuxtLink
        to="/chats"
        class="-ms-3 flex size-11 shrink-0 items-center justify-center rounded-full text-(--ui-text) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
        :aria-label="t.common.back"
        data-testid="back-link"
      >
        <UIcon
          name="i-lucide-chevron-left"
          class="size-6"
          aria-hidden="true"
        />
      </NuxtLink>

      <AppAvatar
        :initials="chat.initials"
        :color="chat.avatarColor"
        :image-id="chat.avatarImageId"
        :icon="chat.icon"
        :size="38"
        :online="chat.isOnline"
        :expand-title="chat.name"
      />

      <div
        class="min-w-0 flex-1"
        data-q2-private
      >
        <h1 class="truncate text-[15px] font-extrabold">
          {{ chat.name }}
        </h1>
        <p class="truncate text-[11px] font-bold text-(--ui-text-muted)">
          {{ status }}
        </p>
      </div>

      <!-- Grey in both states: muting is a state, not something waiting to be
           done. The last button in the row pulls into the edge padding. -->
      <button
        type="button"
        class="flex size-11 shrink-0 items-center justify-center rounded-full text-(--ui-text-muted) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
        :class="{ '-me-2': chat.kind !== 'Group' }"
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

      <!-- Only a group can be left; a direct conversation is between the two
           of you and there would be nothing left of it. -->
      <button
        v-if="chat.kind === 'Group'"
        type="button"
        class="-me-2 flex size-11 shrink-0 items-center justify-center rounded-full text-(--ui-text-muted) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
        :aria-label="t.chats.leaveGroup"
        data-testid="leave-group"
        @click="isLeaveOpen = true"
      >
        <UIcon
          name="i-lucide-log-out"
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
        class="q2-scroll flex flex-1 flex-col gap-2 bg-(--q2-surface) px-[18px] py-3.5"
      >
        <ChatGoalBanner
          v-if="chat.pinnedGoal"
          :goal="chat.pinnedGoal"
          @cheer="cheer()"
        />

        <!-- Always rendered, so the first message animates in rather than
             mounting the whole list; each timeline item carries its own key. -->
        <TransitionGroup
          tag="ul"
          name="q2-message"
          class="flex list-none flex-col gap-1 p-0"
          data-testid="chat-messages"
        >
          <template
            v-for="item in items"
            :key="item.key"
          >
            <ChatBubble
              v-if="item.kind === 'message'"
              :message="item.message"
              :now="now"
              @react="react"
            />

            <ChatEventLine
              v-else-if="item.kind === 'event'"
              :event="item.event"
            />

            <!-- The owner's photograph sits on their side of the thread, a
                 friend's on the other, like the messages around it. -->
            <li
              v-else
              class="my-1.5 w-[85%] list-none"
              :class="item.event.isMine ? 'self-end' : 'self-start'"
              data-testid="chat-proof"
            >
              <ProofCard
                :proof="item.proof"
                :context="chat.pinnedGoal?.title ?? chat.name"
                :busy="isVoting"
                @vote="vote(item.proof.id, $event)"
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
      :max-edge="maxEdge"
      :hand-in="handIn"
    />

    <PhotoCapture
      v-model:open="isPhotoOpen"
      purpose="ChatPhoto"
      @uploaded="onPhotoPicked"
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
