<script setup lang="ts">
/**
 * The streak card at the top of the start screen.
 *
 * The one deliberately loud thing in the app: it is warm rather than green,
 * because it is the only element that is about momentum rather than progress,
 * and it is the first thing somebody sees when they open q2 in the morning.
 *
 * The week is drawn as seven squares, Monday first, in the order the API sends
 * them — the client never works out which end is Monday.
 */
defineProps<{
  streak: number
  week: boolean[]
}>()

const t = useMessages()
</script>

<template>
  <section
    class="relative overflow-hidden rounded-(--q2-radius-xl) px-5 py-4 text-white"
    style="background: linear-gradient(135deg, #f59e0b, #ef6c00); box-shadow: 0 14px 30px -14px rgba(239, 108, 0, 0.7)"
    aria-labelledby="streak-heading"
    data-testid="streak-hero"
  >
    <span
      class="pointer-events-none absolute -end-4 -top-4 text-[120px] leading-none opacity-15"
      aria-hidden="true"
    >🔥</span>

    <h2
      id="streak-heading"
      class="text-xs font-bold tracking-widest uppercase opacity-90"
    >
      {{ t.home.streakLabel }}
    </h2>

    <p
      class="mt-0.5 flex items-baseline gap-2"
      data-q2-private
    >
      <span class="text-[46px] leading-none font-extrabold">{{ streak }}</span>
      <span class="text-[17px] font-bold">{{ t.home.streakDays(streak) }}</span>
    </p>

    <p
      class="mt-2 text-[13px] font-medium opacity-95"
      data-q2-private
    >
      {{ streak > 0 ? t.home.streakEncouragement : t.home.streakStart }}
    </p>

    <!--
      Decorative: the number above already says how long the streak is, and
      seven letters read out one at a time would tell a screen reader user
      nothing they do not already know.
    -->
    <ul
      class="mt-3.5 flex list-none gap-1.5 p-0"
      aria-hidden="true"
      data-q2-block
    >
      <li
        v-for="(active, index) in week"
        :key="index"
        class="flex h-[30px] flex-1 items-center justify-center rounded-(--q2-radius-sm) text-[11px] font-extrabold"
        :class="active ? 'bg-white/30' : 'bg-white/10'"
      >
        {{ t.time.weekdayInitials[index] }}
      </li>
    </ul>
  </section>
</template>
