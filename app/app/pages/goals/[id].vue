<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'
import type { Goal } from '~/api/types'

interface GoalPayload {
  goal: Goal | null
  failure: ApiFailure | null
}

/**
 * A single goal.
 *
 * Loaded on the server so the page is meaningful without JavaScript and a
 * shared link has the right title.
 *
 * The failure travels inside the async data, not in a separate ref: only what
 * `useAsyncData` returns is serialised into the SSR payload, so a ref set
 * during server rendering would be empty again after hydration.
 */
const route = useRoute()
const api = useGoalsApi()
const { report } = useErrorReporter()

const id = computed(() => String(route.params.id))

const { data, status, refresh } = useAsyncData<GoalPayload>(
  () => `goal:${id.value}`,
  async () => {
    try {
      return { goal: await api.get(id.value), failure: null }
    }
    catch (caught) {
      return { goal: null, failure: report(caught, { feature: 'goals', action: 'detail' }) }
    }
  },
  { watch: [id], default: (): GoalPayload => ({ goal: null, failure: null }) },
)

const goal = computed(() => data.value?.goal ?? null)
const failure = computed(() => data.value?.failure ?? null)

// A missing goal is an ordinary outcome of following a stale link, so it gets
// its own calm state rather than the generic "something went wrong".
const isMissing = computed(() => failure.value?.kind === 'notFound' || (!failure.value && !goal.value))
const error = computed(() => (failure.value && failure.value.kind !== 'notFound' ? failure.value : null))

const isLoading = computed(() => status.value === 'pending')
const dueLabel = computed(() => (goal.value ? describeTargetDate(goal.value, new Date()) : null))

useHead({ title: () => goal.value?.title ?? 'Goal' })
</script>

<template>
  <div class="flex flex-col gap-6">
    <UButton
      to="/"
      icon="i-lucide-arrow-left"
      variant="link"
      color="neutral"
      class="self-start px-0"
    >
      Back to goals
    </UButton>

    <div
      v-if="isLoading"
      class="flex flex-col gap-4"
      aria-busy="true"
    >
      <span class="sr-only">Loading goal…</span>
      <USkeleton class="h-8 w-2/3" />
      <USkeleton class="h-24 w-full" />
    </div>

    <AppErrorState
      v-else-if="error"
      :error="error"
      retryable
      @retry="refresh()"
    />

    <article
      v-else-if="goal"
      class="flex flex-col gap-6"
      data-testid="goal-detail"
    >
      <header class="flex flex-col gap-3">
        <h1 class="text-2xl font-semibold text-pretty">
          {{ goal.title }}
        </h1>
        <GoalStatusBadge
          :status="goal.status"
          :overdue="goal.isOverdue"
        />
        <p
          v-if="goal.description"
          class="max-w-prose text-(--ui-text-muted)"
        >
          {{ goal.description }}
        </p>
      </header>

      <UCard>
        <GoalProgress :percent="goal.progressPercent" />
      </UCard>

      <dl class="grid gap-4 sm:grid-cols-2">
        <div v-if="dueLabel">
          <dt class="text-sm text-(--ui-text-muted)">
            Target date
          </dt>
          <dd :class="{ 'text-(--ui-warning) font-medium': goal.isOverdue }">
            {{ formatDate(goal.targetDate!) }} — {{ dueLabel }}
          </dd>
        </div>

        <div v-if="goal.participants.length > 0">
          <dt class="text-sm text-(--ui-text-muted)">
            Participants
          </dt>
          <dd>
            <ul class="flex flex-wrap gap-1.5 p-0">
              <li
                v-for="participant in goal.participants"
                :key="participant"
                class="list-none"
              >
                <UBadge
                  color="neutral"
                  variant="subtle"
                >
                  {{ participant }}
                </UBadge>
              </li>
            </ul>
          </dd>
        </div>
      </dl>
    </article>

    <AppStateMessage
      v-else-if="isMissing"
      icon="i-lucide-compass"
      title="Goal not found"
      description="We could not find that goal. It may have been removed."
      data-testid="goal-not-found"
    >
      <UButton
        to="/"
        icon="i-lucide-house"
      >
        Back to goals
      </UButton>
    </AppStateMessage>
  </div>
</template>
