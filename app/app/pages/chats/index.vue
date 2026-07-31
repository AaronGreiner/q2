<script setup lang="ts">
/**
 * The conversation list, with a search box over it.
 *
 * Search is debounced before it reaches the server: a request per keystroke on
 * a phone connection is a list that flickers between three different answers
 * while somebody is still typing a name.
 */
const t = useMessages()
const now = useNow()

const input = ref('')
const search = ref('')

let debounce: ReturnType<typeof setTimeout> | undefined
watch(input, (value) => {
  clearTimeout(debounce)
  debounce = setTimeout(() => {
    search.value = value
  }, 250)
})

onScopeDispose(() => clearTimeout(debounce))

const { chats, error, isLoading, refresh } = useChats(search)

useHead({ title: () => t.value.chats.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader :title="t.chats.heading" />

    <div class="shrink-0 px-[18px] pt-1 pb-2.5">
      <label
        class="sr-only"
        for="chat-search"
      >{{ t.common.search }}</label>

      <div class="flex h-10 items-center gap-2 rounded-xl border border-(--ui-border) bg-(--q2-surface) px-3">
        <UIcon
          name="i-lucide-search"
          class="size-[17px] shrink-0 text-(--ui-text-dimmed)"
          aria-hidden="true"
        />
        <input
          id="chat-search"
          v-model="input"
          :placeholder="t.chats.searchPlaceholder"
          type="search"
          autocomplete="off"
          class="min-w-0 flex-1 bg-transparent text-sm text-(--ui-text) outline-none"
          data-testid="chat-search"
        >
      </div>
    </div>

    <div class="q2-scroll flex-1 px-3 pb-6">
      <div
        v-if="isLoading"
        class="flex flex-col gap-2 px-1"
        aria-busy="true"
        aria-live="polite"
      >
        <span class="sr-only">{{ t.common.loading }}</span>
        <USkeleton
          v-for="index in 4"
          :key="index"
          class="h-16 w-full rounded-2xl"
        />
      </div>

      <AppErrorState
        v-else-if="error"
        :error="error"
        retryable
        @retry="refresh()"
      />

      <ul
        v-else-if="chats.length > 0"
        class="list-none p-0"
        data-testid="chat-list"
      >
        <li
          v-for="chat in chats"
          :key="chat.id"
        >
          <ChatListRow
            :chat="chat"
            :now="now"
          />
        </li>
      </ul>

      <AppStateMessage
        v-else
        icon="i-lucide-message-circle"
        :title="search ? t.friends.noMatches : t.chats.noChats"
        :description="search ? t.friends.noMatchesHint : t.chats.noChatsHint"
        data-testid="chats-empty"
      />
    </div>
  </div>
</template>
