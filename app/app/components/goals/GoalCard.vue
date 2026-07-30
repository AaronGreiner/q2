<script setup lang="ts">
import type { Goal } from '~/api/types'

/**
 * One goal, as shown in a list.
 *
 * The reference component for this codebase: it takes typed props, derives
 * everything it displays from them, owns no state, fetches nothing, and can be
 * rendered in a test with a plain object. Anything that needs data or
 * navigation belongs to the page composing it.
 */
const props = defineProps<{
  goal: Goal
  /** Injected so rendering is deterministic in tests and identical in SSR. */
  today?: Date
}>()

const referenceDate = computed(() => props.today ?? new Date())
const dueLabel = computed(() => describeTargetDate(props.goal, referenceDate.value))
const participantsLabel = computed(() => describeParticipants(props.goal.participants))
</script>

<template>
  <UCard
    :ui="{ body: 'flex flex-col gap-3' }"
    class="h-full"
    data-testid="goal-card"
  >
    <div class="flex flex-col gap-2">
      <div class="flex items-start justify-between gap-3">
        <!--
          h3: the list is under an h2, so this keeps the heading order intact
          for anyone navigating by headings.
        -->
        <h3 class="font-semibold text-(--ui-text) text-pretty">
          <NuxtLink
            :to="`/goals/${goal.id}`"
            class="hover:underline focus-visible:underline focus-visible:outline-none"
          >
            {{ goal.title }}
          </NuxtLink>
        </h3>

        <GoalStatusBadge
          :status="goal.status"
          :overdue="goal.isOverdue"
        />
      </div>

      <p
        v-if="goal.description"
        class="text-sm text-(--ui-text-muted) line-clamp-2"
      >
        {{ goal.description }}
      </p>
    </div>

    <GoalProgress :percent="goal.progressPercent" />

    <dl class="flex flex-wrap gap-x-4 gap-y-1 text-sm text-(--ui-text-muted)">
      <div
        v-if="dueLabel"
        class="flex items-center gap-1.5"
      >
        <dt class="sr-only">
          Target date
        </dt>
        <UIcon
          name="i-lucide-calendar"
          class="size-4 shrink-0"
          aria-hidden="true"
        />
        <dd :class="{ 'text-(--ui-warning) font-medium': goal.isOverdue }">
          {{ dueLabel }}
        </dd>
      </div>

      <div
        v-if="participantsLabel"
        class="flex items-center gap-1.5"
      >
        <dt class="sr-only">
          Participants
        </dt>
        <UIcon
          name="i-lucide-users"
          class="size-4 shrink-0"
          aria-hidden="true"
        />
        <dd>{{ participantsLabel }}</dd>
      </div>
    </dl>
  </UCard>
</template>
