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
const subtitle = computed(() => goalSubtitle(props.goal, t.value))
</script>

<template>
  <NuxtLink
    :to="`/goals/${goal.id}`"
    class="q2-card block w-52 shrink-0 p-4 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    data-testid="goal-tile"
  >
    <div class="flex items-center justify-between">
      <span
        class="flex size-9 items-center justify-center rounded-(--q2-radius-sm) bg-(--q2-accent-soft) text-(--q2-accent-soft-text)"
        aria-hidden="true"
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
          :size="24"
          stacked
        />
      </div>
    </div>

    <h3 class="mt-3 text-[15px] leading-tight font-extrabold text-pretty">
      {{ goal.title }}
    </h3>
    <p class="mt-0.5 text-[11px] font-semibold text-(--ui-text-muted)">
      {{ subtitle }}
    </p>

    <AppProgressBar
      class="mt-3"
      :percent="goal.progressPercent"
      :height="7"
      :label="t.goals.progressLabel(goal.progressPercent)"
    />

    <div class="mt-1.5 flex items-center justify-between">
      <span class="text-[11px] font-bold text-(--ui-primary)">{{ goal.progressPercent }}%</span>
      <span class="text-[11px] font-semibold text-(--ui-text-muted)">{{ t.rhythm[goal.rhythm] }}</span>
    </div>
  </NuxtLink>
</template>
