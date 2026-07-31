<script setup lang="ts">
/**
 * Friends, requests and suggestions.
 *
 * Tapping the message button on a friend opens the conversation with them if
 * there is one, and otherwise lands on the chat list — creating a conversation
 * is not something this version can do, and pretending otherwise would be
 * worse than the honest fallback.
 */
const t = useMessages()
const now = useNow()
const api = useQ2Api()
const router = useRouter()

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

const { friends, requests, suggestions, error, isLoading, refresh, accept, decline, request } = useFriends(search)

async function onAccept(id: string) {
  await accept(id)
  // The tab bar counts pending requests, and there is now one fewer.
  await refreshNuxtData('profile')
}

async function onDecline(id: string) {
  await decline(id)
  await refreshNuxtData('profile')
}

async function onMessage(personId: string) {
  const chats = await api.chats.list()
  const direct = chats.find(chat => chat.kind === 'Direct' && chat.name === nameOf(personId))

  await router.push(direct ? `/chats/${direct.id}` : '/chats')
}

function nameOf(personId: string): string | undefined {
  return friends.value.find(friend => friend.person.id === personId)?.person.displayName
}

useHead({ title: () => t.value.friends.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader :title="t.friends.heading" />

    <div class="shrink-0 px-[18px] pt-1 pb-2.5">
      <label
        class="sr-only"
        for="friend-search"
      >{{ t.friends.searchPlaceholder }}</label>

      <div class="flex h-11 items-center gap-2 rounded-xl border border-(--ui-border) bg-(--q2-surface) px-3">
        <UIcon
          name="i-lucide-user-plus"
          class="size-[18px] shrink-0 text-(--ui-primary)"
          aria-hidden="true"
        />
        <input
          id="friend-search"
          v-model="input"
          :placeholder="t.friends.searchPlaceholder"
          type="search"
          autocomplete="off"
          class="min-w-0 flex-1 bg-transparent text-sm text-(--ui-text) outline-none"
          data-testid="friend-search"
        >
      </div>
    </div>

    <div class="q2-scroll flex-1 px-[18px] pb-6">
      <div
        v-if="isLoading"
        class="flex flex-col gap-3"
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

      <template v-else>
        <section
          v-if="requests.length > 0"
          class="mb-6"
          aria-labelledby="requests-heading"
        >
          <h2
            id="requests-heading"
            class="mb-2.5 flex items-center gap-2 px-0.5 text-sm font-extrabold"
          >
            {{ t.friends.requests }}
            <span class="flex h-5 min-w-5 items-center justify-center rounded-full bg-(--q2-accent-solid) px-1.5 text-[11px] font-extrabold text-white">
              {{ requests.length }}
            </span>
          </h2>

          <ul class="flex list-none flex-col gap-2.5 p-0">
            <li
              v-for="item in requests"
              :key="item.id"
            >
              <FriendRequestRow
                :request="item"
                @accept="onAccept"
                @decline="onDecline"
              />
            </li>
          </ul>
        </section>

        <section
          v-if="suggestions.length > 0"
          class="mb-6"
          aria-labelledby="suggestions-heading"
        >
          <h2
            id="suggestions-heading"
            class="mb-2.5 px-0.5 text-sm font-extrabold"
          >
            {{ t.friends.suggestions }}
          </h2>

          <ul class="flex list-none flex-col gap-2.5 p-0">
            <li
              v-for="item in suggestions"
              :key="item.id"
            >
              <FriendSuggestionRow
                :suggestion="item"
                @request="request"
              />
            </li>
          </ul>
        </section>

        <section aria-labelledby="friends-heading">
          <h2
            id="friends-heading"
            class="mb-2.5 px-0.5 text-sm font-extrabold"
          >
            {{ t.friends.yours }} · {{ friends.length }}
          </h2>

          <ul
            v-if="friends.length > 0"
            class="flex list-none flex-col p-0"
            data-testid="friend-list"
          >
            <li
              v-for="friend in friends"
              :key="friend.person.id"
            >
              <FriendRow
                :friend="friend"
                :now="now"
                @message="onMessage"
              />
            </li>
          </ul>

          <AppStateMessage
            v-else
            icon="i-lucide-users"
            :title="search ? t.friends.noMatches : t.friends.none"
            :description="search ? t.friends.noMatchesHint : t.friends.noneHint"
            data-testid="friends-empty"
          />
        </section>
      </template>
    </div>
  </div>
</template>
