<script setup lang="ts">
/**
 * The conversation list, with a search box over it and a way to start a new one.
 *
 * Search is debounced before it reaches the server: a request per keystroke on
 * a phone connection is a list that flickers between three different answers
 * while somebody is still typing a name.
 */
const t = useMessages()
const now = useNow()
const api = useQ2Api()
const router = useRouter()
const { report } = useErrorReporter()

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

// The picker only ever offers friends, which is also the server's rule.
const { friends } = useFriends()

const sheet = useTemplateRef<{ reset: () => void }>('sheet')
const isCreating = ref(false)
const isSheetOpen = ref(false)

async function open(work: () => Promise<{ id: string }>, action: string) {
  if (isCreating.value) return

  isCreating.value = true

  try {
    const chat = await work()

    isSheetOpen.value = false
    sheet.value?.reset()
    await router.push(`/chats/${chat.id}`)
  }
  catch (caught) {
    report(caught, { feature: 'chats', action })
  }
  finally {
    isCreating.value = false
  }
}

/**
 * Opening a direct chat is idempotent on the server, so somebody who already
 * has a thread with this person lands in it rather than starting a second.
 */
const onDirect = (personId: string) =>
  open(() => api.chats.startDirect(personId), 'startDirect')

const onGroup = (value: { title: string, emoji: string, memberIds: string[] }) =>
  open(async () => {
    const chat = await api.chats.createGroup(value)
    toast.show(t.value.toast.groupCreated)
    return chat
  }, 'createGroup')

const toast = useToastMessage()

useHead({ title: () => t.value.chats.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader :title="t.chats.heading">
      <template #actions>
        <UButton
          icon="i-lucide-plus"
          size="lg"
          class="min-h-11 rounded-full font-extrabold"
          data-testid="new-chat"
          @click="isSheetOpen = true"
        >
          {{ t.chats.newChat }}
        </UButton>
      </template>
    </AppScreenHeader>

    <div class="shrink-0 px-[18px] pt-1 pb-2.5">
      <AppSearchField
        id="chat-search"
        v-model="input"
        :label="t.common.search"
        :placeholder="t.chats.searchPlaceholder"
        test-id="chat-search"
      />
    </div>

    <div class="q2-scroll flex-1 px-[18px] pb-6">
      <div
        v-if="isLoading"
        class="flex flex-col gap-2"
        aria-busy="true"
        aria-live="polite"
      >
        <span class="sr-only">{{ t.common.loading }}</span>
        <USkeleton
          v-for="index in 4"
          :key="index"
          class="h-16 w-full rounded-(--q2-radius-lg)"
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

    <ChatCreateSheet
      ref="sheet"
      v-model:open="isSheetOpen"
      :friends="friends"
      :submitting="isCreating"
      @direct="onDirect"
      @group="onGroup"
    />
  </div>
</template>
