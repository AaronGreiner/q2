<script setup lang="ts">
import type { ChatPhoto } from '~/api/types'

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
const picture = useImageSource(() => props.photo.imageId)
const viewer = usePhotoViewer()

const failed = ref(false)
const source = computed(() => (failed.value ? null : picture.value))

function enlarge() {
  viewer.open({
    imageId: props.photo.imageId,
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
    <img
      v-if="source"
      crossorigin="use-credentials"
      :src="source"
      alt=""
      decoding="async"
      loading="lazy"
      class="size-full object-cover"
      @error="failed = true"
    >

    <span
      v-else
      class="flex size-full items-center justify-center"
    >
      <UIcon
        name="i-lucide-image-off"
        class="size-5 text-(--ui-text-dimmed)"
        aria-hidden="true"
      />
    </span>
  </button>
</template>
