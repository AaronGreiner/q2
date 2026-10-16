<script setup lang="ts">
/**
 * The streak, as a flame beside your name on the start screen (#121).
 *
 * It used to be a whole card at the top of the list — the number, a ring for
 * today and a week of pills — which put a figure to look at between the
 * person and what there was to do. The number is what people look for, so it
 * sits where the eye lands first and the card is gone.
 *
 * The same badge the card carried: the flame gradient only while there is a
 * streak for it to mean, and grey without one (docs/adr/0028). No "0" beside
 * a grey flame — a number that is always zero for a new person is not
 * information, and the grey already says it.
 *
 * Read out as one sentence rather than an icon and a digit.
 */
const props = defineProps<{
  streak: number
}>()

const t = useMessages()

const isLit = computed(() => props.streak > 0)
</script>

<template>
  <p
    class="flex shrink-0 items-center gap-1.5"
    data-testid="streak-flame"
    :data-lit="isLit ? '' : undefined"
    data-q2-private
  >
    <span class="sr-only">{{ isLit ? t.home.streakDays(streak) : t.home.noStreak }}</span>

    <span
      class="flex size-[30px] items-center justify-center rounded-full"
      :class="isLit
        ? 'bg-linear-to-br from-(--q2-flame-from) to-(--q2-flame-to) text-black'
        : 'bg-(--q2-track) text-(--ui-text-dimmed)'"
      aria-hidden="true"
    >
      <UIcon
        name="i-lucide-flame"
        class="size-4"
      />
    </span>

    <span
      v-if="isLit"
      class="text-[22px] leading-none font-extrabold tracking-[-0.03em] text-(--q2-flame-text)"
      aria-hidden="true"
      data-testid="streak-count"
    >{{ streak }}</span>
  </p>
</template>
