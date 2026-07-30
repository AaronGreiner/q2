<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'
import type { Goal } from '~/api/types'

/**
 * The four states a list of goals can be in, in one place.
 *
 * Presentational only — it receives state and emits intent. Deciding *how* to
 * load or retry belongs to the page, which is what keeps this component
 * testable with plain props.
 */
const props = withDefaults(defineProps<{
  goals: Goal[]
  loading?: boolean
  error?: ApiFailure | null
  emptyTitle?: string
  emptyDescription?: string
  today?: Date
}>(), {
  loading: false,
  error: null,
  emptyTitle: 'No goals yet',
  emptyDescription: 'Create your first goal to start tracking progress together.',
  today: undefined,
})

const emit = defineEmits<{ retry: [] }>()

const isEmpty = computed(() => !props.loading && !props.error && props.goals.length === 0)
</script>

<template>
  <div>
    <!-- Loading: skeletons matching the card layout, so the page does not jump. -->
    <div
      v-if="loading"
      class="grid gap-4 sm:grid-cols-2 xl:grid-cols-3"
      data-testid="goal-list-loading"
      aria-busy="true"
      aria-live="polite"
    >
      <span class="sr-only">Loading goals…</span>
      <USkeleton
        v-for="index in 3"
        :key="index"
        class="h-44 w-full"
      />
    </div>

    <AppErrorState
      v-else-if="error"
      :error="error"
      retryable
      @retry="emit('retry')"
    />

    <AppStateMessage
      v-else-if="isEmpty"
      icon="i-lucide-target"
      :title="emptyTitle"
      :description="emptyDescription"
      data-testid="goal-list-empty"
    />

    <ul
      v-else
      class="grid list-none gap-4 p-0 sm:grid-cols-2 xl:grid-cols-3"
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
  </div>
</template>
