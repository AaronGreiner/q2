<script setup lang="ts">
import type { Goal } from '~/api/types'

/**
 * A goal in the horizontal strip on the start screen.
 *
 * A narrower sibling of `GoalCard` rather than a variant prop: the two share a
 * subject, not a layout, and the alternative — one card with a `compact` flag
 * — ends up as two templates in one file with an `v-if` between them.
 */
const props = defineProps<{ goal: Goal }>()

const t = useMessages()
const schedule = computed(() => scheduleLabel(props.goal.schedule, t.value))
const window = computed(() => props.goal.current)
</script>

<template>
  <NuxtLink
    :to="`/goals/${goal.id}`"
    class="q2-card block w-52 shrink-0 p-4 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    data-testid="goal-tile"
  >
    <div class="flex items-center justify-between">
      <span
        class="flex size-9 items-center justify-center rounded-(--q2-radius-sm) bg-(--ui-bg-accented) text-(--ui-text)"
        aria-hidden="true"
        data-q2-block
      >
        <UIcon
          :name="goalIconName(goal.icon)"
          class="size-5"
        />
      </span>

      <div
        v-if="goal.participants.length > 0"
        class="flex"
      >
        <AppAvatar
          v-for="participant in goal.participants"
          :key="participant.id"
          :initials="participant.initials"
          :color="participant.avatarColor"
          :image-id="participant.avatarImageId"
          :size="24"
          stacked
        />
      </div>
    </div>

    <h3
      class="mt-3 text-[15px] leading-tight font-extrabold text-pretty"
      data-q2-private
    >
      {{ goal.title }}
    </h3>
    <p
      class="mt-0.5 text-[11px] font-semibold text-(--ui-text-muted)"
      data-q2-private
    >
      {{ schedule }}
    </p>

    <AppProgressBar
      v-if="window && window.requiredProofs > 1"
      class="mt-3"
      :percent="windowPercent(window)"
      :height="7"
      :label="windowLabel(window, t)"
    />

    <div class="mt-1.5 flex items-center justify-between gap-2">
      <span
        class="truncate text-[11px] font-bold"
        data-q2-private
      >{{ window ? windowLabel(window, t) : t.status[goal.status] }}</span>

      <span
        v-if="goal.streak > 0"
        class="flex shrink-0 items-center gap-1 text-[11px] font-bold text-(--q2-flame-text)"
        data-q2-private
      >
        <UIcon
          name="i-lucide-flame"
          class="size-3"
          aria-hidden="true"
        />
        {{ goal.streak }}
      </span>
    </div>
  </NuxtLink>
</template>
