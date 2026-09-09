<script setup lang="ts">
import type { DaySummary } from '~/api/types'

/**
 * How much of today is done, as a ring.
 *
 * Sits under the streak because the two answer different questions: the streak
 * is "have I kept this up", the ring is "am I done for today".
 */
defineProps<{
  today: DaySummary
  streak: number
}>()

const t = useMessages()
</script>

<template>
  <section
    class="q2-card flex items-center gap-4 px-5 py-4"
    aria-labelledby="today-progress-heading"
    data-testid="today-progress"
  >
    <AppProgressRing
      :percent="today.percent"
      :size="92"
      :label="t.home.todaySummary(today.done, today.total)"
    >
      <span class="text-[22px] leading-none font-extrabold">{{ today.percent }}%</span>
      <span class="text-[10px] font-semibold text-(--ui-text-muted)">{{ t.home.todayShort }}</span>
    </AppProgressRing>

    <div
      class="min-w-0 flex-1"
      data-q2-private
    >
      <h2
        id="today-progress-heading"
        class="text-[17px] font-extrabold"
      >
        {{ t.home.todayDone }}
      </h2>
      <p class="mt-0.5 text-[13px] text-(--ui-text-muted)">
        {{ t.home.todaySummary(today.done, today.total) }}
      </p>
      <p class="mt-2 flex items-center gap-1.5 text-xs font-bold text-(--q2-flame-text)">
        <UIcon
          name="i-lucide-flame"
          class="size-4"
          aria-hidden="true"
        />
        {{ t.profile.streakBadge(streak) }}
      </p>
    </div>
  </section>
</template>
