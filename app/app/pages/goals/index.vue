<script setup lang="ts">
import type { CreateGoalRequest } from '~/api/types'

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

const { goals, tasks, error, isLoading, refresh, toggleTask, create, isCreating, createError } = useGoals()

const isSheetOpen = ref(false)
const sheet = useTemplateRef('sheet')

const tabs = computed(() => [
  { value: 'today' as const, label: t.value.goals.tabToday },
  { value: 'goals' as const, label: t.value.goals.tabGoals },
])

async function onCreate(request: CreateGoalRequest) {
  const created = await create(request)
  if (!created) return

  sheet.value?.reset()
  isSheetOpen.value = false
  tab.value = 'goals'
}

useHead({ title: () => t.value.goals.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader :title="t.goals.heading">
      <template #actions>
        <UButton
          icon="i-lucide-plus"
          data-testid="open-create-goal"
          @click="isSheetOpen = true"
        >
          {{ t.goals.new }}
        </UButton>
      </template>
    </AppScreenHeader>

    <div class="shrink-0 px-[18px] pt-1 pb-3">
      <AppSegmented
        v-model="tab"
        :options="tabs"
        :label="t.goals.heading"
      />
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
          v-for="index in 3"
          :key="index"
          class="h-20 w-full rounded-2xl"
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
          aria-labelledby="tasks-heading"
        >
          <h2
            id="tasks-heading"
            class="sr-only"
          >
            {{ t.goals.tabToday }}
          </h2>

          <div
            v-if="tasks.length > 0"
            class="flex flex-col gap-2.5"
            data-testid="task-list"
          >
            <GoalTaskRow
              v-for="task in tasks"
              :key="task.id"
              :task="task"
              detailed
              @toggle="toggleTask"
            />
          </div>

          <AppStateMessage
            v-else
            icon="i-lucide-circle-check"
            :title="t.goals.noTasksToday"
            :description="t.goals.noTasksTodayHint"
            data-testid="tasks-empty"
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
              @click="isSheetOpen = true"
            >
              {{ t.create.open }}
            </UButton>
          </AppStateMessage>
        </section>
      </template>
    </div>

    <GoalCreateSheet
      ref="sheet"
      v-model:open="isSheetOpen"
      :submitting="isCreating"
      :error="createError"
      @submit="onCreate"
    />
  </div>
</template>
