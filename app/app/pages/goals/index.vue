<script setup lang="ts">
import type { CreateGoalRequest, Image } from '~/api/types'
import type { PhotoOrigin } from '~/utils/proofSource'

/**
 * The Dos tab — "Dos & Don'ts" on the screen: what is on today, and all of
 * them, under two tabs.
 *
 * The plus in the header is the one way a goal is made. It lives here rather
 * than in the tab bar, because a plus in the tab bar was the only visible way
 * to reach this screen at all, and people read it as "this is where my goals
 * are" rather than "make a new one".
 *
 * The tab lives in the URL rather than in component state, so a view can be
 * shared and survives a reload. `replace`, not `push`: switching tabs is
 * looking at the same screen from another angle, and it should not take four
 * presses of the back button to leave the page.
 *
 * The second tab was `?tab=goals` while it was labelled "Ziele" (#60). That
 * address is still read, so a bookmark from before keeps working, but nothing
 * writes it any more.
 */
type Tab = 'today' | 'all'

const route = useRoute()
const router = useRouter()
const t = useMessages()
const now = useNow()
const zone = useTimeZoneOffset()

/** The reader's day, for a Don't's "seit X Tagen". */
const today = computed(() => localDay(now.value, zone.value))

const tab = computed<Tab>({
  get: () => (route.query.tab === 'all' || route.query.tab === 'goals' ? 'all' : 'today'),
  set: value => router.replace({ query: value === 'all' ? { tab: 'all' } : {} }),
})

/**
 * The create sheet is a query parameter too, so the open sheet survives a
 * reload and closes with the back gesture, which is what a person expects of
 * something that covers the screen.
 */
const isSheetOpen = computed({
  get: () => route.query.create === '1',
  set: (value) => {
    const query = { ...route.query }
    if (value) query.create = '1'
    else delete query.create
    router.replace({ query })
  },
})

const { goals, due, error, isLoading, refresh, create, isCreating, createError } = useGoals()

/** What still wants a photograph first, each group by the deadline closest to now. */
const dueSorted = computed(() => byDeadline(due.value))

// Who can be asked to check a new goal. The sheet only offers friends, which
// is the server's rule too.
const { friends, isLoading: friendsLoading } = useFriends()
const { isDelivering, deliver, maxEdge } = useProofDelivery()
const { isLogging, logUse } = useDontUse()

/** One tap from the row; the row has changed either way, so read it again. */
async function onLog(goalId: string) {
  if (await logUse(goalId)) await refresh()
}

/** Which goal the camera is open for — one camera per screen, never per row. */
const deliveringFor = ref<string | null>(null)

/** The same hand-in as the start screen's — see pages/index.vue. */
async function handIn(image: Image, origin: PhotoOrigin) {
  const goalId = deliveringFor.value
  if (!goalId) return null

  const result = await deliver(goalId, image, origin)
  if (result.status !== 'moved') await refresh()

  return result.status === 'refused' ? result.message : null
}

const sheet = useTemplateRef('sheet')

const tabs = computed(() => [
  { value: 'today' as const, label: t.value.goals.tabToday },
  { value: 'all' as const, label: t.value.goals.tabAll },
])

async function onCreate(request: CreateGoalRequest) {
  const created = await create(request)
  if (!created) return

  sheet.value?.reset()
  await router.replace({ query: { tab: 'all' } })
}

useHead({ title: () => t.value.goals.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <!-- The one way a goal is made. Not repeated in the empty state below:
         two of them would put the screen's loudest control in two places. -->
    <AppScreenHeader :title="t.goals.heading">
      <template #actions>
        <UButton
          icon="i-lucide-plus"
          size="lg"
          :ui="{ base: 'size-11 justify-center rounded-full' }"
          :aria-label="t.create.open"
          data-testid="open-create-goal"
          @click="isSheetOpen = true"
        />
      </template>
    </AppScreenHeader>

    <AppContentPanel>
      <template #toolbar>
        <AppSegmented
          v-model="tab"
          :options="tabs"
          :label="t.goals.heading"
        />
      </template>
      <div
        v-if="isLoading"
        class="flex flex-col gap-3"
        aria-busy="true"
        aria-live="polite"
      >
        <span class="sr-only">{{ t.common.loading }}</span>
        <USkeleton
          v-for="index in 3"
          :key="index"
          class="h-20 w-full rounded-(--q2-radius-lg)"
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
          v-if="tab === 'today'"
          aria-labelledby="due-heading"
        >
          <h2
            id="due-heading"
            class="sr-only"
          >
            {{ t.goals.tabToday }}
          </h2>

          <div
            v-if="dueSorted.length > 0"
            class="flex flex-col gap-2.5"
            data-testid="due-list"
          >
            <template
              v-for="goal in dueSorted"
              :key="goal.id"
            >
              <DontWindowRow
                v-if="goal.kind === 'Dont'"
                :goal="goal"
                :busy="isDelivering || isLogging"
                @deliver="deliveringFor = $event"
                @log="onLog"
              />
              <GoalWindowRow
                v-else
                :goal="goal"
                :now="now"
                :busy="isDelivering"
                @deliver="deliveringFor = $event"
              />
            </template>
          </div>

          <AppStateMessage
            v-else
            icon="i-lucide-circle-check"
            :title="t.goals.nothingDueToday"
            :description="t.goals.nothingDueTodayHint"
            data-testid="due-empty"
          />
        </section>

        <section
          v-else
          aria-labelledby="goals-heading"
        >
          <h2
            id="goals-heading"
            class="sr-only"
          >
            {{ t.goals.tabAll }}
          </h2>

          <ul
            v-if="goals.length > 0"
            class="flex list-none flex-col gap-3 p-0"
            data-testid="goal-list"
          >
            <li
              v-for="goal in goals"
              :key="goal.id"
            >
              <GoalCard
                :goal="goal"
                :today="today"
              />
            </li>
          </ul>

          <AppStateMessage
            v-else
            icon="i-lucide-target"
            :title="t.goals.noGoals"
            :description="t.goals.noGoalsHint"
            data-testid="goals-empty"
          />

          <!-- The way to what has stopped. At the foot of the screen rather
               than in the header: the archive is somewhere you go looking, not
               somewhere you are sent. It stays here with no goals left, because
               that is exactly when everything is in it. -->
          <UButton
            to="/goals/archive"
            class="mt-4 min-h-11 w-full justify-center"
            size="lg"
            color="neutral"
            variant="ghost"
            icon="i-lucide-archive"
            :label="t.goals.archive"
            data-testid="goals-archive-link"
          />
        </section>
      </template>
    </AppContentPanel>

    <GoalCreateSheet
      ref="sheet"
      v-model:open="isSheetOpen"
      :friends="friends"
      :friends-loading="friendsLoading"
      :submitting="isCreating"
      :error="createError"
      @submit="onCreate"
    />

    <PhotoCapture
      :open="deliveringFor !== null"
      purpose="Proof"
      labels-source
      :max-edge="maxEdge"
      :hand-in="handIn"
      @update:open="value => { if (!value) deliveringFor = null }"
    />
  </div>
</template>
