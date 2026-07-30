<script setup lang="ts">
import { isGoalStatus, type CreateGoalRequest, type GoalStatus } from '~/api/types'

/**
 * The dashboard.
 *
 * The page composes the view and owns the wiring — filter state, loading data,
 * reacting to a submit. Rendering is entirely delegated to components, and
 * every rule about goals lives in `useGoals` or on the server.
 */

const route = useRoute()
const router = useRouter()

/**
 * The filter lives in the URL rather than in component state, so a filtered
 * view can be shared and bookmarked and survives a reload.
 *
 * `push`, not `replace`: changing a filter changes what the user is looking at,
 * and the back button is how people undo that. `replace` would keep the history
 * tidy at the cost of making back jump out of the app entirely — which is the
 * more surprising of the two.
 *
 * An unknown or malformed `?status=` value falls back to "all" instead of
 * sending garbage to the API.
 */
const statusFilter = computed<GoalStatus | undefined>({
  get: () => (isGoalStatus(route.query.status) ? route.query.status : undefined),
  set: value => router.push({ query: value ? { status: value } : {} }),
})

const {
  goals,
  isLoading,
  error,
  refresh,
  create,
  isCreating,
  createError,
} = useGoals(statusFilter)

const form = useTemplateRef('form')
const toast = useToast()

const activeCount = computed(() => goals.value.filter(goal => goal.status === 'Active').length)
const completedCount = computed(() => goals.value.filter(goal => goal.status === 'Completed').length)

const emptyCopy = computed(() => statusFilter.value
  ? {
      title: `No ${goalStatusPresentation(statusFilter.value).label.toLowerCase()} goals`,
      description: 'Nothing matches this filter right now. Try another status.',
    }
  : {
      title: 'No goals yet',
      description: 'Create your first goal to start tracking progress together.',
    })

async function onSubmit(request: CreateGoalRequest) {
  const created = await create(request)
  if (!created) return

  form.value?.reset()
  toast.add({
    title: 'Goal created',
    description: created.title,
    icon: 'i-lucide-circle-check',
    color: 'success',
  })
}

useHead({ title: 'Goals' })
</script>

<template>
  <div class="flex flex-col gap-8">
    <section
      class="flex flex-col gap-2"
      aria-labelledby="dashboard-heading"
    >
      <h1
        id="dashboard-heading"
        class="text-2xl font-semibold"
      >
        Shared goals
      </h1>
      <p class="max-w-prose text-(--ui-text-muted)">
        Track what you are working on, on your own or together with friends.
        <span
          v-if="!isLoading && !error"
          data-testid="goal-summary"
        >
          {{ activeCount }} active, {{ completedCount }} completed.
        </span>
      </p>
    </section>

    <section
      class="flex flex-col gap-4"
      aria-labelledby="goals-heading"
    >
      <div class="flex flex-wrap items-center justify-between gap-3">
        <h2
          id="goals-heading"
          class="text-lg font-medium"
        >
          Your goals
        </h2>

        <div class="flex items-center gap-2">
          <GoalStatusFilter v-model="statusFilter" />
          <UButton
            icon="i-lucide-rotate-cw"
            variant="ghost"
            color="neutral"
            size="sm"
            :loading="isLoading"
            aria-label="Reload goals"
            data-testid="reload-goals"
            @click="refresh()"
          />
        </div>
      </div>

      <GoalList
        :goals="goals"
        :loading="isLoading"
        :error="error"
        :empty-title="emptyCopy.title"
        :empty-description="emptyCopy.description"
        @retry="refresh()"
      />
    </section>

    <section
      class="flex flex-col gap-4"
      aria-labelledby="create-heading"
    >
      <h2
        id="create-heading"
        class="text-lg font-medium"
      >
        Add a goal
      </h2>

      <UCard>
        <GoalCreateForm
          ref="form"
          :submitting="isCreating"
          :error="createError"
          @submit="onSubmit"
        />
      </UCard>
    </section>
  </div>
</template>
