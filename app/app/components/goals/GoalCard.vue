<script setup lang="ts">
import type { Goal } from '~/api/types'

/**
 * One goal in the list.
 *
 * The reference component for this codebase: typed props, everything it shows
 * derived from them, no state, no fetching, and it can be rendered in a test
 * from a plain object. Navigation belongs to the page composing it — which is
 * why the whole card is a link rather than a div with a click handler.
 */
const props = defineProps<{ goal: Goal }>()

const t = useMessages()

const subtitle = computed(() => goalSubtitle(props.goal, t.value))
const reminder = computed(() => formatClock(props.goal.reminderAt))
</script>

<template>
  <NuxtLink
    :to="`/goals/${goal.id}`"
    class="q2-card block p-4 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    data-testid="goal-card"
  >
    <div class="flex items-start gap-3">
      <span
        class="flex size-11 shrink-0 items-center justify-center rounded-(--q2-radius-md) bg-(--q2-accent-soft) text-(--q2-accent-soft-text)"
        aria-hidden="true"
      >
        <UIcon
          :name="goalIconName(goal.icon)"
          class="size-6"
        />
      </span>

      <div class="min-w-0 flex-1">
        <div class="flex flex-wrap items-center gap-2">
          <!-- h3: the list sits under an h2, which keeps heading order intact. -->
          <h3 class="text-base font-extrabold text-pretty">
            {{ goal.title }}
          </h3>

          <span
            v-if="goal.isGroup"
            class="rounded-full bg-(--q2-accent-soft) px-1.5 py-0.5 text-[10px] font-extrabold text-(--q2-accent-soft-text)"
          >
            {{ t.goals.group }}
          </span>

          <span
            v-if="goal.isOverdue"
            class="rounded-full bg-(--q2-amber-soft) px-1.5 py-0.5 text-[10px] font-extrabold text-(--q2-amber)"
            data-testid="goal-overdue"
          >
            {{ t.goals.overdue }}
          </span>
        </div>

        <p class="mt-0.5 text-xs font-semibold text-(--ui-text-muted)">
          {{ t.rhythm[goal.rhythm] }} · {{ subtitle }}
        </p>
      </div>

      <div
        v-if="goal.participants.length > 0"
        class="flex ps-2"
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

    <AppProgressBar
      class="mt-3.5"
      :percent="goal.progressPercent"
      :label="t.goals.progressLabel(goal.progressPercent)"
    />

    <div class="mt-2 flex items-center justify-between">
      <span class="text-[13px] font-extrabold text-(--ui-primary)">{{ goal.progressPercent }}%</span>

      <div class="flex items-center gap-3">
        <span class="flex items-center gap-1 text-[11px] font-bold text-(--q2-amber)">
          <UIcon
            name="i-lucide-flame"
            class="size-3.5"
            aria-hidden="true"
          />
          {{ t.goals.streakDays(goal.streak) }}
        </span>

        <span
          v-if="reminder"
          class="flex items-center gap-1 text-[11px] font-semibold text-(--ui-text-muted)"
        >
          <UIcon
            name="i-lucide-bell"
            class="size-3.5"
            aria-hidden="true"
          />
          {{ reminder }}
        </span>
      </div>
    </div>
  </NuxtLink>
</template>
