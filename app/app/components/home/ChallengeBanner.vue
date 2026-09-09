<script setup lang="ts">
import type { ChallengeRoom } from '~/api/types'

/**
 * The daily challenge, as a trailer on the start screen.
 *
 * A trailer and nothing more: the camera is in the room, not here. Somebody who
 * could contribute straight from the banner would never see their friends'
 * covered contributions — and those are the whole reason to join in.
 *
 * "Erledigt" stays on screen after contributing rather than the banner
 * disappearing. The friends' counter is the reason to look in again in the
 * evening, and a banner that vanished the moment you were done would take that
 * with it.
 */
const props = defineProps<{ room: ChallengeRoom }>()

const t = useMessages()
const now = useNow()

const joined = computed(() => props.room.ownEntry !== null)

/** Whole hours left, floored — "noch 2 Stunden" must never read as more than there is. */
const hoursLeft = computed(() => {
  const remaining = new Date(props.room.challenge.expiresAt).getTime() - now.value

  return remaining <= 0 ? null : Math.floor(remaining / 3_600_000)
})

const participation = computed(() => (props.room.friendCount === 0
  ? t.value.challenge.nobody
  : t.value.challenge.participation(props.room.entries.length, props.room.friendCount)))
</script>

<template>
  <NuxtLink
    to="/challenge"
    class="q2-card block px-3.5 py-3 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    data-testid="challenge-banner"
  >
    <div class="flex items-center gap-2">
      <span class="q2-eyebrow min-w-0 flex-1 truncate">{{ t.challenge.heading }}</span>
      <span class="shrink-0 text-[11px] font-bold text-(--ui-text-dimmed)">{{
        hoursLeft === null ? t.challenge.over : t.challenge.remaining(hoursLeft)
      }}</span>
    </div>

    <p class="mt-1.5 text-[15px] leading-snug font-extrabold">
      {{ room.challenge.prompt }}
    </p>

    <div class="mt-2.5 flex items-center gap-2">
      <span class="min-w-0 flex-1 truncate text-[12px] font-semibold text-(--ui-text-muted)">{{ participation }}</span>

      <!--
        The only colour in the banner sits on the invitation, because that is
        the thing to do right now (docs/adr/0015-qdos-design-language.md). Once
        it is done, the state is grey — a state is never the accent.
      -->
      <span
        v-if="joined"
        class="inline-flex shrink-0 items-center gap-1 text-[12px] font-bold text-(--ui-text-muted)"
        data-testid="challenge-banner-done"
      >
        <UIcon
          name="i-lucide-circle-check"
          class="size-4"
          aria-hidden="true"
        />
        {{ t.challenge.joined }}
      </span>

      <span
        v-else
        class="shrink-0 rounded-full bg-(--q2-accent-solid) px-3 py-1 text-[12px] font-extrabold text-(--q2-accent-contrast)"
      >{{ t.challenge.open }}</span>
    </div>
  </NuxtLink>
</template>
