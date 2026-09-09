<script setup lang="ts">
/**
 * The streak card at the top of the start screen.
 *
 * The one deliberately loud thing in the app, and the only surface that is lit
 * rather than black: a streak is the single piece of state worth interrupting
 * the page for, and on black a flame gradient carries further than any card
 * could. It is set in ink rather than white — #0f0f0f on the pale end of the
 * gradient is 12.7:1 and on the hot end 5.8:1, where white would have been 1.5.
 *
 * The flame gradient appears here and nowhere else. It is not the accent, and
 * the two must not be confused: the accent is something you can do, and a
 * streak is something that is true.
 *
 * A streak of zero is not lit. Nothing has been earned yet, so the card is the
 * same black card as everything else and says so — lighting it anyway would
 * spend the app's one loud surface on the absence of the thing it celebrates,
 * and there would be nothing left to show somebody on the day they reach five.
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
    class="relative overflow-hidden rounded-(--q2-radius-xl) px-5 py-5"
    :class="streak > 0 ? '' : 'q2-card'"
    :style="streak > 0
      ? 'background: linear-gradient(142deg, var(--q2-flame-from) 0%, var(--q2-flame-to) 100%); color: #0f0f0f'
      : undefined"
    aria-labelledby="streak-heading"
    data-testid="streak-hero"
    :data-lit="streak > 0 ? '' : undefined"
  >
    <div class="flex items-start justify-between gap-3">
      <h2
        id="streak-heading"
        class="q2-eyebrow"
        :style="streak > 0 ? 'color: rgba(15, 15, 15, 0.72)' : undefined"
      >
        {{ t.home.streakLabel }}
      </h2>

      <UIcon
        name="i-lucide-flame"
        class="size-6 shrink-0"
        :class="streak > 0 ? 'opacity-70' : 'text-(--ui-text-dimmed)'"
        aria-hidden="true"
      />
    </div>

    <p
      class="mt-1.5 flex items-baseline gap-2"
      data-q2-private
    >
      <span class="text-[52px] leading-none font-extrabold tracking-[-0.04em]">{{ streak }}</span>
      <span class="text-[17px] font-extrabold tracking-tight">{{ t.home.streakDays(streak) }}</span>
    </p>

    <p
      class="mt-2 text-[13px] leading-snug font-semibold"
      :class="streak > 0 ? '' : 'text-(--ui-text-muted)'"
      :style="streak > 0 ? 'color: rgba(15, 15, 15, 0.78)' : undefined"
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
      class="mt-4 flex list-none gap-1.5 p-0"
      aria-hidden="true"
      data-q2-block
    >
      <li
        v-for="(active, index) in week"
        :key="index"
        class="flex h-[30px] flex-1 items-center justify-center rounded-(--q2-radius-sm) text-[11px] font-extrabold"
        :class="streak > 0
          ? (active ? 'bg-black/25 text-white' : 'bg-black/[0.07]')
          : (active ? 'bg-(--ui-bg-accented) text-(--ui-text)' : 'bg-(--ui-bg-muted) text-(--ui-text-dimmed)')"
        :style="streak > 0 && !active ? 'color: rgba(15, 15, 15, 0.55)' : undefined"
      >
        {{ t.time.weekdayInitials[index] }}
      </li>
    </ul>
  </section>
</template>
