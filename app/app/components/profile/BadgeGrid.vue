<script setup lang="ts">
import type { Badge, BadgeKey } from '~/api/types'

/**
 * The badge collection on the profile, earned and not (#22).
 *
 * Back now that badges are actually awarded (`BadgeAwarder` on the server);
 * until then it was a shelf of seeded trophies nothing could earn
 * (docs/adr/0032-the-next-step-first.md).
 *
 * Unearned badges are dimmed rather than hidden — a badge you cannot see is
 * not something to aim for — and each says what it takes, because a locked
 * badge without its criterion is a riddle. "Not earned yet" is also said in
 * words, since faded out is not a state a screen reader can convey. A badge is
 * a state, so nothing here is in the accent (docs/adr/0015-qdos-design-language.md).
 */
const props = defineProps<{ badges: Badge[] }>()

const t = useMessages()

/** Each badge has its own icon; the words come from the catalogue. */
const icons: Record<BadgeKey, string> = {
  StreakHero: 'i-lucide-flame',
  EarlyBird: 'i-lucide-sunrise',
  Bookworm: 'i-lucide-book-open',
  KudosGiver: 'i-lucide-hand-heart',
  Marathon: 'i-lucide-medal',
  WeeklyWinner: 'i-lucide-trophy',
}

const earnedCount = computed(() => props.badges.filter(badge => badge.isEarned).length)
</script>

<template>
  <section aria-labelledby="badges-heading">
    <div class="mb-3 flex items-baseline justify-between px-0.5">
      <h2
        id="badges-heading"
        class="text-base font-extrabold"
      >
        {{ t.badges.heading }}
      </h2>
      <span
        class="text-[13px] font-bold text-(--ui-text-muted)"
        data-testid="badge-count"
      >
        {{ t.badges.earned(earnedCount, badges.length) }}
      </span>
    </div>

    <ul
      class="grid list-none grid-cols-3 gap-2.5 p-0"
      data-testid="badge-grid"
      data-q2-block
    >
      <li
        v-for="badge in badges"
        :key="badge.key"
        class="q2-card flex flex-col items-center px-1.5 py-3.5 text-center"
        :class="badge.isEarned ? '' : 'opacity-50'"
        :data-testid="`badge-${badge.key}`"
        :data-earned="badge.isEarned"
      >
        <span
          class="flex size-11 items-center justify-center rounded-full"
          :class="badge.isEarned
            ? 'bg-(--ui-bg-accented) text-(--ui-text)'
            : 'bg-(--q2-track) text-(--ui-text-muted)'"
          aria-hidden="true"
        >
          <UIcon
            :name="icons[badge.key]"
            class="size-6"
          />
        </span>

        <span class="mt-2 text-[12px] leading-tight font-extrabold">
          {{ t.badges.name[badge.key] }}
        </span>
        <span class="mt-0.5 text-[11px] leading-tight font-semibold text-(--ui-text-muted)">
          {{ t.badges.criterion[badge.key] }}
        </span>

        <!-- The dimming is decorative; this is what actually says "not yet". -->
        <span
          v-if="!badge.isEarned"
          class="sr-only"
        >{{ t.badges.locked }}</span>
      </li>
    </ul>
  </section>
</template>
