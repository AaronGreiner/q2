<script setup lang="ts">
import type { ProofMediaParts } from '~/utils/proofMedia'

/**
 * A stored proof — photograph or clip, and the front camera's picture — drawn
 * by `ProofMediaFrame` (#124, #125).
 *
 * Only the addresses are worked out here, through `useImageSource`, so the iOS
 * app fetches each part with its token like any other picture. A tile never
 * plays its clip, so it never asks for the clip's bytes either: only for the
 * still.
 */
const props = withDefaults(defineProps<{
  parts: ProofMediaParts
  fit?: 'cover' | 'contain'
  mode?: 'inline' | 'controls' | 'still'
  openable?: boolean
  openLabel?: string
  alt?: string
  insetCorner?: 'top-start' | 'bottom-end'
}>(), {
  fit: 'cover',
  mode: 'inline',
  openable: false,
  openLabel: undefined,
  alt: '',
  insetCorner: 'top-start',
})

const emit = defineEmits<{ open: [] }>()

const isVideo = computed(() => props.parts.media === 'Video')

const main = useImageSource(() => (isVideo.value && props.mode === 'still' ? null : props.parts.imageId))
const poster = useImageSource(() => (isVideo.value ? props.parts.posterImageId : null))
const inset = useImageSource(() => props.parts.insetImageId)
</script>

<template>
  <ProofMediaFrame
    :kind="parts.media"
    :src="main"
    :poster-src="poster"
    :inset-src="inset"
    :fit="fit"
    :mode="mode"
    :openable="openable"
    :open-label="openLabel"
    :alt="alt"
    :inset-corner="insetCorner"
    @open="emit('open')"
  />
</template>
