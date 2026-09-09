<script setup lang="ts">
import { imageUrl } from '~/api/images'
import type { ChallengeArchiveEntry } from '~/api/types'

/**
 * One of your own past contributions, cropped square.
 *
 * Only the date is on the tile. The prompt appears underneath in the row's
 * accessible label and on the detail — in a three-column grid it would be too
 * long, and without it somewhere the picture is unplaceable in six months.
 */
const props = defineProps<{ item: ChallengeArchiveEntry }>()

const t = useMessages()
const { public: config } = useRuntimeConfig()

const failed = ref(false)

const source = computed(() =>
  (failed.value || !props.item.entry.imageId
    ? null
    : imageUrl(config.apiBaseUrl, props.item.entry.imageId)))

const day = computed(() => formatInstantDate(props.item.challenge.publishedAt))
</script>

<template>
  <figure
    class="relative m-0 aspect-square overflow-hidden rounded-(--q2-radius-md) border border-(--ui-border) bg-(--ui-bg-elevated)"
    data-testid="challenge-archive-tile"
    data-q2-block
  >
    <img
      v-if="source"
      :src="source"
      :alt="item.challenge.prompt"
      crossorigin="use-credentials"
      decoding="async"
      loading="lazy"
      class="size-full object-cover"
      @error="failed = true"
    >

    <div
      v-else
      class="flex size-full items-center justify-center"
    >
      <UIcon
        name="i-lucide-image-off"
        class="size-6 text-(--ui-text-dimmed)"
        aria-hidden="true"
      />
    </div>

    <!--
      A drop shadow rather than a bar: the picture stays whole, and the date
      still reads over whatever is behind it.
    -->
    <figcaption
      class="absolute start-1.5 bottom-1 text-[10px] font-extrabold text-white [text-shadow:0_1px_3px_rgb(0_0_0/0.9)]"
    >
      {{ day }}
      <span class="sr-only">— {{ item.challenge.prompt }}</span>
    </figcaption>

    <span class="sr-only">{{ t.challenge.archive }}</span>
  </figure>
</template>
