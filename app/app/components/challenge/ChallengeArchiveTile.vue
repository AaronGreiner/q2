<script setup lang="ts">
import type { ChallengeArchiveEntry } from '~/api/types'
import { proofMediaOf } from '~/utils/proofMedia'

/**
 * One of your own past contributions, cropped square, with what it answered.
 *
 * The date sits on the picture and the prompt underneath it, cut to two lines.
 * Without the prompt the picture is unplaceable in six months: a photograph
 * answers a question, and the question is what makes it mean anything. The
 * whole prompt, uncut, is in the full-screen view a tap opens. A clip shows
 * its still with a play mark, and the front camera's part sits in the bottom
 * corner, as on every tile (`ProofMedia`, #152).
 */
const props = defineProps<{ item: ChallengeArchiveEntry }>()

const t = useMessages()
const viewer = usePhotoViewer()

const media = computed(() => (props.item.entry.imageId
  ? proofMediaOf({ ...props.item.entry, imageId: props.item.entry.imageId })
  : null))

const day = computed(() => formatInstantDate(props.item.challenge.publishedAt))

function enlarge() {
  if (!media.value) return

  viewer.open({
    ...media.value,
    title: props.item.challenge.prompt,
    meta: day.value,
  })
}
</script>

<template>
  <figure
    class="m-0 flex min-w-0 flex-col gap-1"
    data-testid="challenge-archive-tile"
    data-q2-block
  >
    <component
      :is="media ? 'button' : 'div'"
      :type="media ? 'button' : undefined"
      class="relative block aspect-square w-full overflow-hidden rounded-(--q2-radius-md) border border-(--ui-border) bg-(--ui-bg-elevated) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="media ? t.challenge.enlarge(item.challenge.prompt) : undefined"
      data-testid="challenge-archive-enlarge"
      @click="media ? enlarge() : undefined"
    >
      <ProofMedia
        v-if="media"
        :parts="media"
        mode="still"
        inset-corner="bottom-end"
        :alt="item.challenge.prompt"
      />

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
      <span
        class="absolute start-1.5 bottom-1 text-[10px] font-extrabold text-white [text-shadow:0_1px_3px_rgb(0_0_0/0.9)]"
      >{{ day }}</span>
    </component>

    <figcaption
      class="line-clamp-2 px-0.5 text-[11px] leading-tight font-semibold text-(--ui-text-muted)"
      data-testid="challenge-archive-prompt"
    >
      {{ item.challenge.prompt }}
    </figcaption>
  </figure>
</template>
