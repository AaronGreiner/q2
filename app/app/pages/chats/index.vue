<script setup lang="ts">
/**
 * The conversation list, with a search box over it and a way to start a new one.
 *
 * Search is debounced before it reaches the server: a request per keystroke on
 * a phone connection is a list that flickers between three different answers
 * while somebody is still typing a name.
 *
 * Three sections, because the rows are three different jobs: your own goals,
 * where you deliver; friends' goals, where you check; and conversations about
 * no goal at all. Each keeps the server's order — newest first — and search
 * runs across all of them (docs/adr/0027-goal-conversations.md).
 *
 * The conversations of goals that have ended are in none of them, but folded
 * away at the end under "Beendet" (#101): every goal keeps its conversation,
 * and after a year the two goal sections would otherwise be mostly finished
 * things, with what needs attention further down.
 */
const t = useMessages()
const now = useNow()
const api = useQ2Api()
const router = useRouter()
const { report } = useErrorReporter()

const input = ref('')
const search = ref('')

/*
 * What was typed is still there when this screen is gone back to from one of
 * its results (#118) — kept with the visit, not in the address
 * (useReturnState).
 */
const typed = useReturnState('chatSearch', () => input.value.trim() ? input.value : undefined)
if (typed) {
  input.value = typed
  search.value = typed
}

let debounce: ReturnType<typeof setTimeout> | undefined
watch(input, (value) => {
  clearTimeout(debounce)
  debounce = setTimeout(() => {
    search.value = value
  }, 250)
})

onScopeDispose(() => clearTimeout(debounce))

const { chats, error, isLoading, refresh } = useChats(search)

const running = computed(() => chats.value.filter(chat => !chat.goalEnded))
const ended = computed(() => chats.value.filter(chat => chat.goalEnded))

const sections = computed(() => [
  {
    key: 'mine',
    title: t.value.chats.sectionMine,
    hint: t.value.chats.sectionMineHint,
    chats: running.value.filter(chat => chat.kind === 'Goal' && chat.isMyGoal),
  },
  {
    key: 'friends',
    title: t.value.chats.sectionFriends,
    hint: t.value.chats.sectionFriendsHint,
    chats: running.value.filter(chat => chat.kind === 'Goal' && !chat.isMyGoal),
  },
  {
    key: 'conversations',
    title: t.value.chats.sectionConversations,
    hint: null,
    chats: running.value.filter(chat => chat.kind !== 'Goal'),
  },
].filter(section => section.chats.length > 0))

/*
 * Folded until somebody opens it, and kept open when they come back from one
 * of its threads (#118). A search shows it unfolded: what was typed is looked
 * for everywhere, and a match hidden behind a fold would read as no match.
 * Something unread in it is said on the fold itself, so a new message in an
 * ended goal's conversation is not missed.
 */
const isEndedOpen = ref(false)
if (useReturnState('chatEndedOpen', () => isEndedOpen.value || undefined)) isEndedOpen.value = true

const isSearching = computed(() => search.value.trim().length > 0)
const showEnded = computed(() => isEndedOpen.value || isSearching.value)
const endedUnread = computed(() => ended.value.reduce((total, chat) => total + chat.unreadCount, 0))

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

const onGroup = (value: { title: string, icon: string, memberIds: string[] }) =>
  open(() => api.chats.createGroup(value), 'createGroup')

useHead({ title: () => t.value.chats.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader :title="t.chats.heading">
      <template #actions>
        <!-- An outline: the filled accent belongs to the tab bar's create
             button, which is on this screen too and is the louder of the two. -->
        <UButton
          icon="i-lucide-plus"
          size="lg"
          variant="outline"
          class="min-h-11 rounded-full font-extrabold"
          data-testid="new-chat"
          @click="isSheetOpen = true"
        >
          {{ t.chats.newChat }}
        </UButton>
      </template>
    </AppScreenHeader>

    <ChatContactStrip
      :people="friends.map(friend => friend.person)"
      :busy="isCreating"
      @open="onDirect"
    />

    <AppContentPanel :refresh="refresh">
      <template #toolbar>
        <AppSearchField
          id="chat-search"
          v-model="input"
          :label="t.common.search"
          :placeholder="t.chats.searchPlaceholder"
          test-id="chat-search"
        />
      </template>
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

      <div
        v-else-if="chats.length > 0"
        class="flex flex-col gap-4"
        data-testid="chat-list"
      >
        <section
          v-for="section in sections"
          :key="section.key"
          :aria-labelledby="`chat-section-${section.key}`"
          :data-testid="`chat-section-${section.key}`"
        >
          <h2
            :id="`chat-section-${section.key}`"
            class="flex items-baseline justify-between gap-2 px-0.5 pb-1"
          >
            <span class="q2-eyebrow">{{ section.title }}</span>
            <span
              v-if="section.hint"
              class="text-[11px] font-semibold text-(--ui-text-dimmed)"
            >{{ section.hint }}</span>
          </h2>

          <ul class="list-none p-0">
            <li
              v-for="chat in section.chats"
              :key="chat.id"
            >
              <ChatListRow
                :chat="chat"
                :now="now"
              />
            </li>
          </ul>
        </section>

        <section
          v-if="ended.length > 0"
          aria-labelledby="chat-section-ended"
          data-testid="chat-section-ended"
        >
          <!-- While searching there is nothing to fold: the results are shown
               under a plain heading, like the other sections. -->
          <h2
            v-if="isSearching"
            id="chat-section-ended"
            class="flex items-baseline justify-between gap-2 px-0.5 pb-1"
          >
            <span class="q2-eyebrow">{{ t.chats.sectionEnded }}</span>
            <span class="text-[11px] font-semibold text-(--ui-text-dimmed)">{{ t.chats.sectionEndedHint }}</span>
          </h2>

          <h2
            v-else
            id="chat-section-ended"
          >
            <button
              type="button"
              class="flex min-h-11 w-full items-center justify-between gap-2 rounded-(--q2-radius-md) px-0.5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
              :aria-expanded="isEndedOpen"
              aria-controls="chat-ended-list"
              data-testid="chat-ended-toggle"
              @click="isEndedOpen = !isEndedOpen"
            >
              <span class="flex items-center gap-2">
                <span class="q2-eyebrow">{{ t.chats.sectionEnded }} · {{ ended.length }}</span>
                <span
                  v-if="endedUnread > 0"
                  class="flex h-5 min-w-5 items-center justify-center rounded-full bg-(--q2-accent-solid) px-1.5 text-[11px] font-extrabold text-(--q2-accent-contrast)"
                  data-testid="chat-ended-unread"
                >{{ endedUnread }}</span>
              </span>
              <span class="flex items-center gap-1.5 text-[11px] font-semibold text-(--ui-text-dimmed)">
                {{ t.chats.sectionEndedHint }}
                <UIcon
                  name="i-lucide-chevron-down"
                  class="size-4 transition-transform duration-(--q2-motion-normal)"
                  :class="isEndedOpen ? 'rotate-180' : ''"
                  aria-hidden="true"
                />
              </span>
            </button>
          </h2>

          <ul
            v-show="showEnded"
            id="chat-ended-list"
            class="list-none p-0"
          >
            <li
              v-for="chat in ended"
              :key="chat.id"
            >
              <ChatListRow
                :chat="chat"
                :now="now"
              />
            </li>
          </ul>
        </section>
      </div>

      <AppStateMessage
        v-else
        icon="i-lucide-message-circle"
        :title="search ? t.friends.noMatches : t.chats.noChats"
        :description="search ? t.friends.noMatchesHint : t.chats.noChatsHint"
        data-testid="chats-empty"
      />
    </AppContentPanel>

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
