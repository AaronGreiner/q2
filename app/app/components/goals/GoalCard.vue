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

const schedule = computed(() => scheduleLabel(props.goal.schedule, t.value))
const reminder = computed(() => formatClock(props.goal.reminderAt))

/** What the open window still wants, or nothing when there is none. */
const window = computed(() => props.goal.current)

/**
 * The line under the card: the window while there is one, otherwise why there
 * is not. A paused goal has no open window, and "Abgeschlossen" would be a lie
 * about a goal that is merely resting.
 */
const statusLine = computed(() => {
  if (window.value) return windowLabel(window.value, t.value)

  return props.goal.pause
    ? t.value.pause.until(formatDay(props.goal.pause.endsOn))
    : t.value.status[props.goal.status]
})
</script>

<template>
  <NuxtLink
    :to="`/goals/${goal.id}`"
    class="q2-card block p-4 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    data-testid="goal-card"
  >
    <div class="flex items-start gap-3">
      <span
        class="flex size-11 shrink-0 items-center justify-center rounded-(--q2-radius-md) bg-(--ui-bg-accented) text-(--ui-text)"
        aria-hidden="true"
        data-q2-block
      >
        <UIcon
          :name="goalIconName(goal.icon)"
          class="size-6"
        />
      </span>

      <div class="min-w-0 flex-1">
        <div class="flex flex-wrap items-center gap-2">
          <!-- h3: the list sits under an h2, which keeps heading order intact. -->
          <h3
            class="text-base font-extrabold text-pretty"
            data-q2-private
          >
            {{ goal.title }}
          </h3>

          <span
            v-if="goal.isGroup"
            class="rounded-full bg-(--ui-bg-accented) px-1.5 py-0.5 text-[10px] font-extrabold text-(--ui-text-muted)"
          >
            {{ t.goals.group }}
          </span>

          <span
            v-if="goal.isOverdue"
            class="rounded-full bg-(--q2-flame-soft) px-1.5 py-0.5 text-[10px] font-extrabold text-(--q2-flame-text)"
            data-testid="goal-overdue"
          >
            {{ t.goals.overdue }}
          </span>

          <!-- Grey, and never the flame. A pause is not a failure and not
               something to do; it is the absence of both. -->
          <span
            v-if="goal.pause"
            class="rounded-full bg-(--ui-bg-accented) px-1.5 py-0.5 text-[10px] font-extrabold text-(--ui-text-muted)"
            data-testid="goal-paused"
          >
            {{ t.pause.bannerTitle }}
          </span>
        </div>

        <p
          class="mt-0.5 text-xs font-semibold text-(--ui-text-muted)"
          data-q2-private
        >
          {{ schedule }}
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
          :image-id="participant.avatarImageId"
          :size="24"
          stacked
        />
      </div>
    </div>

    <!-- The bar is the open window, not the goal: a goal has no percentage
         any more, and "3 von 4 diese Woche" is a period with a result where
         "14 von 21" was a counter somebody turned up. -->
    <AppProgressBar
      v-if="window && window.requiredProofs > 1"
      class="mt-3.5"
      :percent="windowPercent(window)"
      :label="windowLabel(window, t)"
    />

    <div class="mt-2 flex items-center justify-between">
      <span
        class="text-[13px] font-extrabold"
        data-testid="goal-window"
        data-q2-private
      >{{ statusLine }}</span>

      <div class="flex items-center gap-3">
        <span
          class="flex items-center gap-1 text-[11px] font-bold text-(--q2-flame-text)"
          data-q2-private
        >
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
          data-q2-private
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
