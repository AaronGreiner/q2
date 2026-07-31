<script setup lang="ts">
/**
 * One conversation.
 *
 * Opening it marks it read on the server, which is why the navigation badge is
 * refreshed afterwards — the count lives on the profile, and it has just
 * changed under it.
 */
definePageMeta({ layout: 'plain' })

const route = useRoute()
const t = useMessages()
const now = useNow()

const id = computed(() => String(route.params.id))
const { chat, error, isMissing, isLoading, refresh, send, cheer, react, isSending } = useChatThread(id)

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
      class="flex shrink-0 items-center gap-2.5 border-b border-(--ui-border) bg-(--q2-surface) px-3 py-2"
    >
      <NuxtLink
        to="/chats"
        class="flex size-9 shrink-0 items-center justify-center rounded-full text-(--ui-text) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
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

      <div class="min-w-0 flex-1">
        <h1 class="truncate text-[15px] font-extrabold">
          {{ chat.name }}
        </h1>
        <p class="truncate text-[11px] font-bold text-(--ui-primary)">
          {{ status }}
        </p>
      </div>
    </header>

    <div
      v-if="isLoading"
      class="flex-1 p-4"
      aria-busy="true"
      aria-live="polite"
    >
      <span class="sr-only">{{ t.common.loading }}</span>
      <USkeleton class="h-14 w-2/3 rounded-2xl" />
      <USkeleton class="mt-3 ms-auto h-14 w-1/2 rounded-2xl" />
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
        class="q2-scroll flex flex-1 flex-col gap-1 p-3.5"
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
  </div>
</template>
