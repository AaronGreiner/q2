<script setup lang="ts">
import type { ChallengeRoom } from '~/api/types'

/**
 * The daily challenge on the start screen: the prompt, and what your friends
 * made of it.
 *
 * It used to be a trailer, a line of text that led into the room, because the
 * room was the only place the pictures were and a banner you could contribute
 * from would have skipped them (#135). The pictures are here now, as a strip of
 * tiles, so the camera is too: "Mitmachen" asks the screen to open it, and the
 * friends' pictures appear in the strip as soon as the contribution is in.
 *
 * Covered is still covered (docs/adr/0021-daily-challenge.md). Before you have
 * contributed, a friend's tile shows who it is and nothing of what they did —
 * the server sends no picture, so there is nothing here to blur.
 *
 * The room stays one tap away, through the heading: reacting, replacing and
 * removing are done there, where a picture is large enough to applaud.
 */
const props = withDefaults(defineProps<{
  room: ChallengeRoom
  /** While a contribution is on its way. */
  busy?: boolean
}>(), {
  busy: false,
})

const emit = defineEmits<{ join: [] }>()

const t = useMessages()
const now = useNow()

const joined = computed(() => props.room.ownEntry !== null)
const covered = computed(() => !props.room.isRevealed)

/**
 * The minutes left of the moment while it lasts, then "zu spät" with the
 * hours left of the day (#136). Whole hours floored — "noch 2 Stunden" must
 * never read as more than there is.
 */
const clock = computed(() => challengeClock(props.room.challenge, now.value, t.value))

const participation = computed(() => (props.room.friendCount === 0
  ? t.value.challenge.nobody
  : t.value.challenge.participation(props.room.entries.length, props.room.friendCount)))

/*
 * Yours first, because it is what uncovered the rest; then your friends',
 * newest first. The room lists them oldest first, like a chat that fills up
 * over the day, but a strip is cut off at the right edge, and what is cut off
 * should be what you already saw at lunchtime.
 */
const tiles = computed(() => [
  ...(props.room.ownEntry ? [props.room.ownEntry] : []),
  ...[...props.room.entries].reverse(),
])
</script>

<template>
  <section
    class="q2-card overflow-hidden"
    aria-labelledby="challenge-card-heading"
    data-testid="challenge-card"
  >
    <NuxtLink
      to="/challenge"
      class="block px-4 pt-3.5 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-(--ui-primary)"
      data-testid="challenge-card-room"
    >
      <div class="flex items-center gap-2">
        <h2
          id="challenge-card-heading"
          class="q2-eyebrow min-w-0 flex-1 truncate"
        >
          {{ t.challenge.heading }}
        </h2>
        <span
          class="shrink-0 text-[11px] font-bold text-(--ui-text-dimmed)"
          data-testid="challenge-card-clock"
        >{{ clock }}</span>
        <UIcon
          name="i-lucide-chevron-right"
          class="-me-1 size-4 shrink-0 text-(--ui-text-dimmed)"
          aria-hidden="true"
        />
      </div>

      <!-- The prompt is what the card is about, and is set like a headline. -->
      <p
        class="mt-1.5 text-xl leading-tight font-extrabold tracking-tight"
        data-testid="challenge-card-prompt"
      >
        {{ room.challenge.prompt }}
      </p>
    </NuxtLink>

    <!--
      The strip runs to the card's edges so a tile cut off at the right says
      "there is more" by itself; the padding is inside the scroll area, and
      snapping keeps a flick from stopping halfway through a picture.
    -->
    <ul
      v-if="tiles.length > 0"
      class="q2-scroll-x mt-3 flex snap-x snap-mandatory scroll-px-4 gap-2 px-4"
      :aria-label="covered ? t.challenge.covered : t.challenge.friends"
      data-testid="challenge-strip"
    >
      <li
        v-for="entry in tiles"
        :key="entry.id"
        class="w-29 shrink-0 snap-start"
      >
        <ChallengeStripTile
          :entry="entry"
          :covered="covered && !entry.isMine"
          :prompt="room.challenge.prompt"
        />
      </li>
    </ul>

    <div class="flex flex-col gap-3 px-4 pt-3 pb-4">
      <div class="flex items-center gap-2">
        <span class="min-w-0 flex-1 truncate text-[12px] font-semibold text-(--ui-text-muted)">{{ participation }}</span>

        <!--
          Once you are in, being in is a state, and a state is grey
          (docs/adr/0015-qdos-design-language.md).
        -->
        <span
          v-if="joined"
          class="inline-flex shrink-0 items-center gap-1 text-[12px] font-bold text-(--ui-text-muted)"
          data-testid="challenge-card-done"
        >
          <UIcon
            name="i-lucide-circle-check"
            class="size-4"
            aria-hidden="true"
          />
          {{ t.challenge.joined }}
        </span>
      </div>

      <!-- The only colour on the card, because it is the thing to do now. -->
      <UButton
        v-if="!joined"
        icon="i-lucide-camera"
        size="xl"
        class="q2-press min-h-12 justify-center rounded-full font-extrabold"
        :loading="busy"
        data-testid="challenge-card-join"
        @click="emit('join')"
      >
        {{ t.challenge.open }}
      </UButton>
    </div>
  </section>
</template>
