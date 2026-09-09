<script setup lang="ts">
/**
 * Search: find anybody, and below that the people you already know — who is
 * waiting on an answer, who you have asked, and who you might know.
 *
 * This is where friends live now that the tab bar's fifth slot is the create
 * button. It is the right place for them: you find a person by looking for
 * them, and the requests waiting for you are the first thing on the screen.
 *
 * The search box has one meaning — find a person, anywhere in q2 — and while
 * something is typed into it the sections are replaced by its results. A box
 * that filtered your own friends *and* was the only way to add somebody was the
 * reason this screen could not add anybody it had not already been told about.
 *
 * The message button opens the conversation with somebody, creating it if there
 * is not one yet. That is one call, and it is idempotent on the server, so
 * tapping it twice lands in the same thread rather than making two.
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

const {
  friends,
  requests,
  sentRequests,
  suggestions,
  error,
  isLoading,
  refresh,
  accept,
  decline,
  request,
  withdraw,
  remove,
} = useFriends()

const {
  results,
  isSearching,
  isActive: isSearchActive,
  minimumLength,
  error: searchError,
  search: runSearch,
} = usePersonSearch(search)

/** Whether the typed-in box is showing results instead of the usual sections. */
const isSearchMode = computed(() => search.value.trim().length > 0)

async function onMessage(personId: string) {
  try {
    const chat = await api.chats.startDirect(personId)
    await router.push(`/chats/${chat.id}`)
  }
  catch (caught) {
    report(caught, { feature: 'friends', action: 'startChat' })
  }
}

/**
 * Removing ends the friendship for both people, so it asks first. The person
 * being removed is held while the dialog is open rather than passed through it,
 * so the dialog stays a dumb yes/no.
 */
const pendingRemoval = ref<{ id: string, name: string } | null>(null)
const isRemoveOpen = ref(false)

function onRemove(personId: string) {
  const friend = friends.value.find(entry => entry.person.id === personId)
  if (!friend) return

  pendingRemoval.value = { id: personId, name: friend.person.displayName }
  isRemoveOpen.value = true
}

async function confirmRemove() {
  const target = pendingRemoval.value
  pendingRemoval.value = null

  if (target) await remove(target.id)
}

/** A search result changes state on every action, so the list is re-fetched. */
async function actOnResult(personId: string, action: (id: string) => Promise<void>) {
  await action(personId)
  await runSearch()
}

useHead({ title: () => t.value.friends.pageHeading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader :title="t.friends.pageHeading" />

    <div class="shrink-0 px-[18px] pt-1 pb-2.5">
      <AppSearchField
        id="friend-search"
        v-model="input"
        :label="t.friends.searchPlaceholder"
        :placeholder="t.friends.searchPlaceholder"
        icon="i-lucide-user-plus"
        accent
        test-id="friend-search"
      />
    </div>

    <div class="q2-scroll flex-1 px-[18px] pb-6">
      <!-- Searching: the sections make way for what was asked for. -->
      <section
        v-if="isSearchMode"
        aria-labelledby="search-heading"
        data-testid="person-search"
      >
        <h2
          id="search-heading"
          class="q2-eyebrow mb-3 px-0.5"
        >
          {{ t.friends.searchHeading }}
        </h2>

        <div
          v-if="isSearching"
          class="flex flex-col gap-2.5"
          aria-busy="true"
          aria-live="polite"
        >
          <span class="sr-only">{{ t.common.loading }}</span>
          <USkeleton
            v-for="index in 3"
            :key="index"
            class="h-16 w-full rounded-(--q2-radius-lg)"
          />
        </div>

        <AppErrorState
          v-else-if="searchError"
          :error="searchError"
          retryable
          @retry="runSearch()"
        />

        <ul
          v-else-if="results.length > 0"
          class="flex list-none flex-col gap-2.5 p-0"
          data-testid="person-results"
        >
          <li
            v-for="result in results"
            :key="result.person.id"
          >
            <PersonSearchRow
              :result="result"
              @request="id => actOnResult(id, request)"
              @withdraw="id => actOnResult(id, withdraw)"
              @accept="id => actOnResult(id, accept)"
              @message="onMessage"
            />
          </li>
        </ul>

        <AppStateMessage
          v-else
          icon="i-lucide-search"
          :title="isSearchActive ? t.friends.noMatches : t.friends.searchHeading"
          :description="isSearchActive ? t.friends.noMatchesHint : t.friends.searchHint(minimumLength)"
          data-testid="search-empty"
        />
      </section>

      <template v-else>
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
            class="h-16 w-full rounded-(--q2-radius-lg)"
          />
        </div>

        <AppErrorState
          v-else-if="error"
          :error="error"
          retryable
          @retry="refresh()"
        />

        <template v-else>
          <!--
            The first thing on this screen for somebody who knows nobody here.

            Above the requests and suggestions rather than below, because for a
            new account both of those are empty and the screen would otherwise
            open on nothing at all. It disappears the moment there is a single
            friend: an invite card on a full screen is furniture.
          -->
          <InviteCard
            v-if="friends.length === 0 && requests.length === 0"
            class="mb-6"
          />

          <section
            v-if="requests.length > 0"
            class="mb-6"
            aria-labelledby="requests-heading"
          >
            <h2
              id="requests-heading"
              class="q2-eyebrow mb-3 flex items-center gap-2 px-0.5"
            >
              {{ t.friends.requests }}
              <span class="flex h-5 min-w-5 items-center justify-center rounded-full bg-(--ui-bg-accented) px-1.5 text-[11px] font-extrabold text-(--ui-text)">
                {{ requests.length }}
              </span>
            </h2>

            <ul class="flex list-none flex-col gap-2.5 p-0">
              <li
                v-for="item in requests"
                :key="item.person.id"
              >
                <FriendRequestRow
                  :request="item"
                  @accept="accept"
                  @decline="decline"
                />
              </li>
            </ul>
          </section>

          <section
            v-if="sentRequests.length > 0"
            class="mb-6"
            aria-labelledby="sent-heading"
          >
            <h2
              id="sent-heading"
              class="q2-eyebrow mb-3 px-0.5"
            >
              {{ t.friends.sentRequests }}
            </h2>

            <ul class="flex list-none flex-col gap-2.5 p-0">
              <li
                v-for="item in sentRequests"
                :key="item.person.id"
              >
                <SentRequestRow
                  :request="item"
                  @withdraw="withdraw"
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
              class="q2-eyebrow mb-3 px-0.5"
            >
              {{ t.friends.suggestions }}
            </h2>

            <ul class="flex list-none flex-col gap-2.5 p-0">
              <li
                v-for="item in suggestions"
                :key="item.person.id"
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
              class="q2-eyebrow mb-3 px-0.5"
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
                  @remove="onRemove"
                />
              </li>
            </ul>

            <AppStateMessage
              v-else
              icon="i-lucide-users"
              :title="t.friends.none"
              :description="t.friends.noneHint"
              data-testid="friends-empty"
            />
          </section>
        </template>
      </template>
    </div>

    <AppConfirmDialog
      v-if="pendingRemoval"
      v-model:open="isRemoveOpen"
      :title="t.friends.remove"
      :description="t.friends.removeConfirm(pendingRemoval.name)"
      :confirm-label="t.friends.remove"
      private-description
      @confirm="confirmRemove"
    />
  </div>
</template>
