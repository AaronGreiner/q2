<script setup lang="ts">
import type { StyleValue } from 'vue'
import type { ProofMediaParts } from '~/utils/proofMedia'

/**
 * A stored proof — photograph or clip, and the front camera's picture or clip
 * — drawn by `ProofMediaFrame` (#124, #125, #151).
 *
 * Only the addresses are worked out here, through `useImageSource`, so the iOS
 * app fetches each part with its token like any other picture. A tile never
 * plays a clip, so it never asks for a clip's bytes either: only for the
 * stills.
 */
const props = withDefaults(defineProps<{
  parts: ProofMediaParts
  fit?: 'cover' | 'contain'
  mode?: 'inline' | 'controls' | 'still'
  openable?: boolean
  openLabel?: string
  alt?: string
  insetCorner?: 'top-start' | 'bottom-end'
  insetTop?: string
  insetTopEnd?: string
  zoomStyle?: StyleValue
}>(), {
  fit: 'cover',
  mode: 'inline',
  openable: false,
  openLabel: undefined,
  alt: '',
  insetCorner: 'top-start',
  insetTop: undefined,
  insetTopEnd: undefined,
  zoomStyle: undefined,
})

const emit = defineEmits<{ open: [] }>()

const swapped = defineModel<boolean>('swapped', { default: false })

const isVideo = computed(() => props.parts.media === 'Video')
const insetIsVideo = computed(() => props.parts.insetMedia === 'Video')

const main = useImageSource(() => (isVideo.value && props.mode === 'still' ? null : props.parts.imageId))
const poster = useImageSource(() => (isVideo.value ? props.parts.posterImageId : null))
const inset = useImageSource(() => (insetIsVideo.value && props.mode === 'still' ? null : props.parts.insetImageId))
const insetPoster = useImageSource(() => (insetIsVideo.value ? props.parts.insetPosterImageId : null))
</script>

<template>
  <ProofMediaFrame
    v-model:swapped="swapped"
    :kind="parts.media"
    :src="main"
    :poster-src="poster"
    :inset-kind="parts.insetMedia"
    :inset-src="inset"
    :inset-poster-src="insetPoster"
    :fit="fit"
    :mode="mode"
    :openable="openable"
    :open-label="openLabel"
    :alt="alt"
    :inset-corner="insetCorner"
    :inset-top="insetTop"
    :inset-top-end="insetTopEnd"
    :zoom-style="zoomStyle"
    @open="emit('open')"
  />
</template>
