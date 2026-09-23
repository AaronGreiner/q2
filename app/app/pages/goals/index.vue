<script setup lang="ts">
import type { CreateGoalRequest, Image } from '~/api/types'

/**
 * Goals, under two tabs: what is on today, and the goals themselves.
 *
 * The tab lives in the URL rather than in component state, so a view can be
 * shared and survives a reload. `replace`, not `push`: switching tabs is
 * looking at the same screen from another angle, and it should not take four
 * presses of the back button to leave the page.
 */
type Tab = 'today' | 'goals'

const route = useRoute()
const router = useRouter()
const t = useMessages()

const tab = computed<Tab>({
  get: () => (route.query.tab === 'goals' ? 'goals' : 'today'),
  set: value => router.replace({ query: value === 'goals' ? { tab: 'goals' } : {} }),
})

/**
 * The create sheet is a query parameter too, because the tab bar's centre
 * button links here rather than reaching into this page's state. That also
 * makes the open sheet survive a reload and close with the back gesture, which
 * is what a person expects of something that covers the screen.
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

// Who can be asked to check a new goal. The sheet only offers friends, which
// is the server's rule too.
const { friends, isLoading: friendsLoading } = useFriends()
const { isDelivering, deliver, maxEdge } = useProofDelivery()

/** Which goal the camera is open for — one sheet per screen, never per row. */
const deliveringFor = ref<string | null>(null)

/** The same hand-in as the start screen's — see pages/index.vue. */
async function handIn(image: Image) {
  const goalId = deliveringFor.value
  if (!goalId) return null

  const result = await deliver(goalId, image)
  if (result.status !== 'moved') await refresh()

  return result.status === 'refused' ? result.message : null
}

const sheet = useTemplateRef('sheet')

const tabs = computed(() => [
  { value: 'today' as const, label: t.value.goals.tabToday },
  { value: 'goals' as const, label: t.value.goals.tabGoals },
])

async function onCreate(request: CreateGoalRequest) {
  const created = await create(request)
  if (!created) return

  sheet.value?.reset()
  await router.replace({ query: { tab: 'goals' } })
}

useHead({ title: () => t.value.goals.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <!-- No create button here: the tab bar's centre button is the one way in,
         and two of them would put the screen's loudest control in two places. -->
    <AppScreenHeader :title="t.goals.heading" />

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
            v-if="due.length > 0"
            class="flex flex-col gap-2.5"
            data-testid="due-list"
          >
            <GoalWindowRow
              v-for="goal in due"
              :key="goal.id"
              :goal="goal"
              :busy="isDelivering"
              @deliver="deliveringFor = $event"
            />
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
            {{ t.goals.tabGoals }}
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
              <GoalCard :goal="goal" />
            </li>
          </ul>

          <AppStateMessage
            v-else
            icon="i-lucide-target"
            :title="t.goals.noGoals"
            :description="t.goals.noGoalsHint"
            data-testid="goals-empty"
          >
            <UButton
              icon="i-lucide-plus"
              data-testid="open-create-goal"
              @click="isSheetOpen = true"
            >
              {{ t.create.open }}
            </UButton>
          </AppStateMessage>

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
      :max-edge="maxEdge"
      :hand-in="handIn"
      @update:open="value => { if (!value) deliveringFor = null }"
    />
  </div>
</template>
