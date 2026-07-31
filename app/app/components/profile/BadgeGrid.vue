<script setup lang="ts">
import type { Badge, BadgeKey } from '~/api/types'

/**
 * The badge collection, earned and not.
 *
 * Unearned badges are dimmed rather than hidden — a badge you cannot see is
 * not something to aim for — and every one of them says so in words, because
 * "faded out" is not a state a screen reader can convey.
 */
defineProps<{ badges: Badge[] }>()

const t = useMessages()

/** Each badge has its own icon; the label comes from the catalogue. */
const icons: Record<BadgeKey, string> = {
  StreakHero: 'i-lucide-flame',
  EarlyBird: 'i-lucide-sunrise',
  Bookworm: 'i-lucide-book-open',
  KudosGiver: 'i-lucide-hand-heart',
  Marathon: 'i-lucide-medal',
  WeeklyWinner: 'i-lucide-trophy',
}
</script>

<template>
  <ul
    class="grid list-none grid-cols-3 gap-2.5 p-0"
    data-testid="badge-grid"
  >
    <li
      v-for="badge in badges"
      :key="badge.key"
      class="q2-card flex flex-col items-center px-1.5 py-3.5"
      :class="badge.isEarned ? '' : 'opacity-45'"
    >
      <span
        class="flex size-11 items-center justify-center rounded-full"
        :class="badge.isEarned
          ? 'bg-(--q2-accent-soft) text-(--q2-accent-soft-text)'
          : 'bg-(--q2-track) text-(--ui-text-muted)'"
        aria-hidden="true"
      >
        <UIcon
          :name="icons[badge.key]"
          class="size-6"
        />
      </span>

      <span class="mt-2 text-center text-[11px] leading-tight font-bold">
        {{ t.badges[badge.key] }}
      </span>

      <!-- The dimming is decorative; this is what actually says "not yet". -->
      <span
        v-if="!badge.isEarned"
        class="sr-only"
      >{{ t.profile.badgeLocked }}</span>
    </li>
  </ul>
</template>
