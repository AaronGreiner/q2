<script setup lang="ts">
import type { ChatPhoto } from '~/api/types'
import { proofMediaOf } from '~/utils/proofMedia'

/**
 * One photograph in a conversation's grid (#24), square, opening full size.
 *
 * Cropped to the square on purpose — a grid of differently shaped tiles is
 * hard to scan — and whole in the full-screen view a tap opens, with who sent
 * it and when, and for a proof whether it was live (ADR 0036).
 */
const props = defineProps<{
  photo: ChatPhoto
  /** The conversation's name, which is what the picture belongs to. */
  context: string
}>()

const t = useMessages()
const viewer = usePhotoViewer()

/** A proof may be a clip, with the front camera's picture beside it (#124, #125). */
const media = computed(() => proofMediaOf(props.photo))

function enlarge() {
  viewer.open({
    ...media.value,
    title: props.context,
    subtitle: props.photo.authorName,
    meta: formatInstantDate(props.photo.at),
    source: props.photo.isProof ? props.photo.source : null,
  })
}
</script>

<template>
  <button
    type="button"
    class="relative block aspect-square w-full overflow-hidden rounded-(--q2-radius-sm) bg-(--ui-bg-elevated) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    :aria-label="t.chats.photoBy(photo.authorName)"
    data-testid="chat-photo-tile"
    data-q2-block
    @click="enlarge()"
  >
    <ProofMedia
      :parts="media"
      mode="still"
    />
  </button>
</template>
