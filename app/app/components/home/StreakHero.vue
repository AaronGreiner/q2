<script setup lang="ts">
/** The streak stays prominent in the quiet surface; only its flame carries the gradient. */
defineProps<{ streak: number, week: boolean[] }>()
const t = useMessages()
</script>

<template>
  <UCard
    class="rounded-none border-b border-(--ui-border-muted) bg-transparent shadow-none ring-0"
    :ui="{ body: 'px-1 pt-1 pb-5 sm:px-1 sm:pt-1 sm:pb-5' }"
    aria-labelledby="streak-heading"
    data-testid="streak-hero"
    :data-lit="streak > 0 ? '' : undefined"
  >
    <div class="flex items-center justify-between gap-3">
      <h2
        id="streak-heading"
        class="q2-eyebrow"
      >
        {{ t.home.streakLabel }}
      </h2>
      <span
        class="flex size-9 items-center justify-center rounded-full"
        :class="streak > 0 ? 'bg-linear-to-br from-(--q2-flame-from) to-(--q2-flame-to) text-black' : 'bg-(--q2-track) text-(--ui-text-dimmed)'"
        data-testid="streak-flame"
        aria-hidden="true"
      >
        <UIcon
          name="i-lucide-flame"
          class="size-5"
        />
      </span>
    </div>
    <p
      class="mt-1 flex items-baseline gap-2"
      data-q2-private
    >
      <span class="text-[42px] leading-none font-extrabold tracking-[-0.04em]">{{ streak }}</span>
      <span class="text-base font-semibold">{{ t.home.streakDays(streak) }}</span>
    </p>
    <p
      class="mt-2 text-[13px] leading-relaxed text-(--ui-text-muted)"
      data-q2-private
    >
      {{ streak > 0 ? t.home.streakEncouragement : t.home.streakStart }}
    </p>
    <!-- The server supplies Monday-first activity; the client only draws it. -->
    <ul
      class="mt-4 flex list-none gap-1.5 p-0"
      aria-hidden="true"
      data-q2-block
    >
      <li
        v-for="(active, index) in week"
        :key="index"
        class="flex h-7 flex-1 items-center justify-center rounded-full text-[11px] font-semibold"
        :class="active ? 'bg-(--ui-bg-accented) text-(--ui-text)' : 'bg-(--ui-bg-muted) text-(--ui-text-dimmed)'"
      >
        {{ t.time.weekdayInitials[index] }}
      </li>
    </ul>
  </UCard>
</template>
