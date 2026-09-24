<script setup lang="ts">
import type { DaySummary } from '~/api/types'

/**
 * The streak, today and this week, in one card.
 *
 * It used to be two cards that both said the streak — "0 Tage in Folge" and,
 * directly below, "0-Tage-Streak" — with a week of pills that only spelled out
 * the days. Now the number is said once, today's share sits beside it as a
 * ring, and each pill answers something: kept (a tick), today (outlined), or
 * still to come (faint). Only the flame carries the gradient, and only while
 * there is a streak for it to mean.
 */
const props = defineProps<{
  streak: number
  /** Monday-first: whether something was kept on each day of this week. */
  week: boolean[]
  today: DaySummary
  /** Today's position in `week`, from the reader's clock. */
  todayIndex: number
}>()

const t = useMessages()

const days = computed(() => weekStates(props.week, props.todayIndex))
const activeDays = computed(() => props.week.filter(Boolean).length)
</script>

<template>
  <section
    class="q2-card px-4 pt-4 pb-4"
    aria-labelledby="streak-heading"
    data-testid="streak-hero"
    :data-lit="streak > 0 ? '' : undefined"
  >
    <div class="flex items-start justify-between gap-3">
      <div class="min-w-0">
        <h2
          id="streak-heading"
          class="q2-eyebrow flex items-center gap-1.5"
        >
          <span
            class="flex size-6 items-center justify-center rounded-full"
            :class="streak > 0 ? 'bg-linear-to-br from-(--q2-flame-from) to-(--q2-flame-to) text-black' : 'bg-(--q2-track) text-(--ui-text-dimmed)'"
            data-testid="streak-flame"
            aria-hidden="true"
          >
            <UIcon
              name="i-lucide-flame"
              class="size-3.5"
            />
          </span>
          {{ t.home.streakLabel }}
        </h2>

        <p
          class="mt-2 flex items-baseline gap-2"
          data-q2-private
        >
          <span class="text-[42px] leading-none font-extrabold tracking-[-0.04em]">{{ streak }}</span>
          <span class="text-base font-semibold">{{ t.home.streakDays(streak) }}</span>
        </p>
      </div>

      <div data-testid="today-progress">
        <AppProgressRing
          :percent="today.percent"
          :size="68"
          :label="t.home.todaySummary(today.done, today.total)"
        >
          <span
            class="text-[15px] leading-none font-extrabold"
            data-q2-private
          >{{ today.done }}/{{ today.total }}</span>
          <span class="text-[10px] font-semibold text-(--ui-text-muted)">{{ t.home.todayShort }}</span>
        </AppProgressRing>
      </div>
    </div>

    <p
      class="mt-2 text-[13px] leading-relaxed text-(--ui-text-muted)"
      data-q2-private
    >
      {{ streak > 0 ? t.home.streakEncouragement : t.home.streakStart }}
    </p>

    <!-- The server supplies Monday-first activity; the client only draws it.
         Read out as one sentence rather than seven letters. -->
    <p class="sr-only">
      {{ t.home.weekSummary(activeDays) }}
    </p>
    <ul
      class="mt-4 flex list-none gap-1.5 p-0"
      aria-hidden="true"
      data-q2-block
      data-testid="streak-week"
    >
      <li
        v-for="(state, index) in days"
        :key="index"
        class="flex h-7 flex-1 items-center justify-center rounded-full text-[11px] font-bold"
        :class="{
          'bg-(--ui-text) text-(--ui-bg)': state === 'done' || state === 'todayDone',
          'ring-2 ring-(--ui-text) ring-offset-2 ring-offset-(--ui-bg-muted)': state === 'todayDone',
          'bg-(--ui-bg-accented) text-(--ui-text) ring-2 ring-(--ui-text) ring-inset': state === 'today',
          'bg-(--ui-bg-accented) text-(--ui-text-dimmed)': state === 'past',
          'bg-transparent text-(--ui-text-dimmed) ring-1 ring-(--ui-border) ring-inset': state === 'future',
        }"
        :data-state="state"
      >
        <UIcon
          v-if="state === 'done' || state === 'todayDone'"
          name="i-lucide-check"
          class="size-3.5"
        />
        <template v-else>
          {{ t.time.weekdayInitials[index] }}
        </template>
      </li>
    </ul>
  </section>
</template>
