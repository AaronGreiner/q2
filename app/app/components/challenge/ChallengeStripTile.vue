<script setup lang="ts">
import type { ChallengeEntry } from '~/api/types'

/**
 * One contribution in the start screen's strip, as a portrait tile.
 *
 * The small sibling of `ChallengeEntryCard`: a picture, whose it is, and a tap
 * that opens it full screen. Reacting, replacing and removing stay in the room,
 * where there is space for them — a row of buttons under a tile 116 pixels wide
 * would be a row of things to miss.
 *
 * Covered means what it means in the room (docs/adr/0021-daily-challenge.md):
 * the name is shown and the picture is not, and there is no picture behind the
 * cover, because the server did not send one.
 */
const props = defineProps<{
  entry: ChallengeEntry
  /** Covered while the viewer has not contributed. */
  covered: boolean
  /** The day's challenge, for the full-screen view. */
  prompt: string
}>()

const t = useMessages()
const now = useNow()
const picture = useImageSource(() => (props.covered ? null : props.entry.imageId))
const viewer = usePhotoViewer()

const failed = ref(false)

watch(() => props.entry.imageId, () => {
  failed.value = false
})

const source = computed(() =>
  (props.covered || failed.value || !props.entry.imageId
    ? null
    : picture.value))

/** "Du" on your own, so the first tile in the strip says why it is first. */
const name = computed(() => (props.entry.isMine ? t.value.challenge.you : props.entry.author.displayName))

function enlarge() {
  if (!props.entry.imageId) return

  viewer.open({
    imageId: props.entry.imageId,
    title: props.prompt,
    subtitle: props.entry.author.displayName,
    meta: formatRelativeTime(props.entry.createdAt, now.value, t.value),
  })
}
</script>

<template>
  <component
    :is="source ? 'button' : 'div'"
    :type="source ? 'button' : undefined"
    class="relative block aspect-4/5 w-full overflow-hidden rounded-(--q2-radius-md) bg-(--ui-bg-elevated) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    :aria-label="source ? t.challenge.enlargeEntry(entry.author.displayName) : undefined"
    :data-covered="covered ? '' : undefined"
    data-testid="challenge-tile"
    data-q2-block
    @click="source ? enlarge() : undefined"
  >
    <img
      v-if="source"
      crossorigin="use-credentials"
      :src="source"
      :alt="t.challenge.entryAlt(entry.author.displayName)"
      decoding="async"
      class="size-full object-cover"
      data-testid="challenge-tile-image"
      @error="failed = true"
    >

    <!--
      Covered: who it is, large, and nothing of what they did. Seeing who is
      already in is the reason to join (docs/adr/0021-daily-challenge.md), so
      the face is the tile, and the closed eye in the corner says why the
      picture is not.
    -->
    <div
      v-else-if="covered"
      class="flex size-full flex-col items-center justify-center gap-2 px-2"
    >
      <UIcon
        name="i-lucide-eye-off"
        class="absolute end-2 top-2 size-3.5 text-(--ui-text-dimmed)"
        aria-hidden="true"
      />
      <AppAvatar
        :initials="entry.author.initials"
        :color="entry.author.avatarColor"
        :image-id="entry.author.avatarImageId"
        :size="44"
        aria-hidden="true"
      />
      <span
        class="w-full truncate text-center text-[11px] font-extrabold text-(--ui-text-muted)"
        aria-hidden="true"
        data-testid="challenge-tile-name"
        data-q2-private
      >{{ name }}</span>
      <span class="sr-only">{{ t.challenge.coveredEntry(entry.author.displayName) }}</span>
    </div>

    <!-- A picture that should be there and did not load. -->
    <div
      v-else
      class="flex size-full items-center justify-center pb-7 text-(--ui-text-dimmed)"
    >
      <UIcon
        name="i-lucide-image-off"
        class="size-5"
        aria-hidden="true"
      />
    </div>

    <!--
      Who it is, on the picture rather than under it, so the strip is one row
      of pictures and not a row of pictures and a row of captions: white with
      a drop shadow, as on the archive tiles.
    -->
    <span
      v-if="!covered"
      class="absolute inset-x-1.5 bottom-1.5 flex items-center gap-1.5"
      :aria-hidden="source ? 'true' : undefined"
    >
      <AppAvatar
        :initials="entry.author.initials"
        :color="entry.author.avatarColor"
        :image-id="entry.author.avatarImageId"
        :size="20"
        class="shrink-0"
      />
      <span
        class="min-w-0 truncate text-[11px] font-extrabold"
        :class="source ? 'text-white [text-shadow:0_1px_3px_rgb(0_0_0/0.9)]' : 'text-(--ui-text-muted)'"
        data-testid="challenge-tile-name"
        data-q2-private
      >{{ name }}</span>
    </span>
  </component>
</template>
