<script setup lang="ts">
/**
 * One conversation.
 *
 * Opening it marks it read on the server, which is why the navigation badge is
 * refreshed afterwards — the count lives on the profile, and it has just
 * changed under it.
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
const { chat, error, isMissing, isLoading, refresh, send, cheer, react, leave, isSending } = useChatThread(id)

const thread = useTemplateRef<HTMLElement>('thread')

/** A new message belongs at the bottom, where a thread is read from. */
async function scrollToLatest() {
  await nextTick()
  if (thread.value) thread.value.scrollTop = thread.value.scrollHeight
}

const status = computed(() => {
  if (!chat.value) return ''
  if (chat.value.kind === 'Group') return t.value.chats.members(chat.value.memberCount)
  if (chat.value.isOnline) return t.value.chats.online
  if (chat.value.otherLastSeenAt) {
    return t.value.chats.lastSeen(formatRelativeTime(chat.value.otherLastSeenAt, now.value, t.value))
  }
  return t.value.chats.offline
})

async function onSend(text: string) {
  await send(text)
  await scrollToLatest()
}

/**
 * Leaving is not undoable from inside the app — somebody in the group would
 * have to start a new one — so it asks first.
 */
const isLeaveOpen = ref(false)

onMounted(async () => {
  await scrollToLatest()

  // The badge in the tab bar counts unread conversations, and this one is not
  // one any more.
  await refreshNuxtData('profile')
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
        :size="38"
        :online="chat.isOnline"
      />

      <div
        class="min-w-0 flex-1"
        data-q2-private
      >
        <h1 class="truncate text-[15px] font-extrabold">
          {{ chat.name }}
        </h1>
        <p class="truncate text-[11px] font-bold text-(--ui-primary)">
          {{ status }}
        </p>
      </div>

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
        class="q2-scroll flex flex-1 flex-col gap-1 px-[18px] py-3.5"
      >
        <ChatGoalBanner
          v-if="chat.pinnedGoal"
          :goal="chat.pinnedGoal"
          @cheer="cheer()"
        />

        <ul
          v-if="chat.messages.length > 0"
          class="flex list-none flex-col gap-1 p-0"
          data-testid="chat-messages"
        >
          <ChatBubble
            v-for="message in chat.messages"
            :key="message.id"
            :message="message"
            :now="now"
            @react="react"
          />
        </ul>

        <AppStateMessage
          v-else
          class="my-auto"
          icon="i-lucide-message-circle"
          :title="t.chats.empty"
          :description="t.chats.emptyHint"
        />
      </div>

      <ChatComposer
        :busy="isSending"
        @send="onSend"
      />
    </template>

    <AppConfirmDialog
      v-model:open="isLeaveOpen"
      :title="t.chats.leaveGroup"
      :description="t.chats.leaveGroupConfirm"
      :confirm-label="t.chats.leaveGroup"
      @confirm="leave"
    />
  </div>
</template>
