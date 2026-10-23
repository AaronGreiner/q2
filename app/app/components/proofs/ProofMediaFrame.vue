<script setup lang="ts">
import type { MediaKind } from '~/api/types'

/**
 * What a proof shows, drawn from addresses: a photograph or a clip, and the
 * front camera's picture in a corner (#124, #125).
 *
 * The one place that knows how a proof is laid out, so the card, the full
 * screen, the gallery and the camera's own preview cannot disagree about it.
 * It is handed addresses rather than ids — `ProofMedia` resolves stored ones,
 * the camera passes the `blob:` addresses of what it is still holding.
 *
 * **The corner swaps.** Tapping the front picture makes it the large one and
 * puts the main one in the corner — a clip by its still, because a second
 * playing video in a corner is noise. Where the frame is itself a button (a
 * tile), the corner is only shown, never pressed.
 *
 * **A clip plays without sound until somebody asks for it** (#124). On a card
 * it plays while it is on screen and pauses when it scrolls away, with one
 * button for the sound; full screen it has the player's own controls; on a
 * tile it does not play at all and shows its still with a play mark.
 *
 * **Everything here is somebody's face or room**, so the whole frame is
 * replaced in Session Replay (`data-q2-block`, ADRs 0005 and 0017) — the clip,
 * the still and the front picture alike.
 */
const props = withDefaults(defineProps<{
  kind: MediaKind
  /** The photograph or the clip. */
  src: string | null
  /** A clip's still. */
  posterSrc?: string | null
  /** The front camera's picture. */
  insetSrc?: string | null
  /** `contain` where the whole picture matters — a screenshot, the full screen. */
  fit?: 'cover' | 'contain'
  /** `inline` on a card, `controls` full screen, `still` on a tile. */
  mode?: 'inline' | 'controls' | 'still'
  /** Whether the large picture opens something — the card's full-screen view. */
  openable?: boolean
  /** The accessible name of that button. */
  openLabel?: string
  alt?: string
  /** Where the front picture sits: a tile keeps its top corner for its status. */
  insetCorner?: 'top-start' | 'bottom-end'
}>(), {
  posterSrc: null,
  insetSrc: null,
  fit: 'cover',
  mode: 'inline',
  openable: false,
  openLabel: undefined,
  alt: '',
  insetCorner: 'top-start',
})

const emit = defineEmits<{ open: [] }>()

const t = useMessages()

const swapped = ref(false)
const muted = ref(true)
const failed = ref(false)

const root = useTemplateRef<HTMLElement>('root')
const player = useTemplateRef<HTMLVideoElement>('player')

watch(() => [props.src, props.insetSrc], () => {
  swapped.value = false
  failed.value = false
})

const isVideo = computed(() => props.kind === 'Video')

/** The still that stands for the main picture where a clip cannot play. */
const mainStill = computed(() => (isVideo.value ? props.posterSrc : props.src))

/** A clip plays in the large slot, unless it is a tile or has been swapped into the corner. */
const plays = computed(() => isVideo.value && props.mode !== 'still' && !swapped.value && !!props.src)

const largeImage = computed(() => (swapped.value ? props.insetSrc : mainStill.value))
const cornerImage = computed(() => (swapped.value ? mainStill.value : props.insetSrc))

const fitClass = computed(() => (props.fit === 'contain' ? 'object-contain' : 'object-cover'))

/*
 * On a card a clip plays while at least half of it is on screen. Not
 * `autoplay`: a conversation can hold a week of clips, and every one of them
 * playing — and, in the browser, loading — at once is a phone getting warm.
 */
let observer: IntersectionObserver | null = null

watch(player, (element) => {
  observer?.disconnect()
  observer = null

  if (!element || props.mode !== 'inline' || typeof IntersectionObserver === 'undefined') return

  observer = new IntersectionObserver(([entry]) => {
    if (entry?.isIntersecting) element.play().catch(() => {})
    else element.pause()
  }, { threshold: 0.5 })

  observer.observe(root.value ?? element)
})

onBeforeUnmount(() => observer?.disconnect())

function toggleSound() {
  muted.value = !muted.value

  // Asking for the sound is asking for the clip, even one scrolled half away.
  if (!muted.value) player.value?.play().catch(() => {})
}
</script>

<template>
  <div
    ref="root"
    class="relative size-full overflow-hidden"
    :data-media="kind"
    data-testid="proof-media"
    data-q2-block
  >
    <component
      :is="openable ? 'button' : 'div'"
      :type="openable ? 'button' : undefined"
      class="block size-full focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="openable ? openLabel : undefined"
      :data-testid="openable ? 'proof-enlarge' : undefined"
      @click="openable && emit('open')"
    >
      <!--
        `playsinline` keeps iOS from taking the clip full screen on its own.
        `crossorigin` before `:src`, as for every picture (api/images.ts).
      -->
      <video
        v-if="plays && !failed"
        ref="player"
        crossorigin="use-credentials"
        :src="src ?? undefined"
        :poster="posterSrc ?? undefined"
        playsinline
        loop
        preload="metadata"
        :muted="muted"
        :autoplay="mode === 'controls'"
        :controls="mode === 'controls'"
        class="size-full"
        :class="fitClass"
        data-testid="proof-video"
        @error="failed = true"
      />

      <img
        v-else-if="largeImage && !failed"
        crossorigin="use-credentials"
        :src="largeImage"
        :alt="alt"
        decoding="async"
        draggable="false"
        class="size-full"
        :class="fitClass"
        data-testid="proof-image"
        @error="failed = true"
      >

      <span
        v-else
        class="flex size-full items-center justify-center"
      >
        <UIcon
          name="i-lucide-image-off"
          class="size-8 text-(--ui-text-dimmed)"
          aria-hidden="true"
        />
      </span>
    </component>

    <!-- The other picture, in the corner the thumb does not cover. -->
    <component
      :is="mode === 'still' ? 'span' : 'button'"
      v-if="cornerImage"
      :type="mode === 'still' ? undefined : 'button'"
      class="absolute block aspect-[3/4] w-[28%] max-w-32 overflow-hidden rounded-(--q2-radius-sm) bg-(--ui-bg-elevated) shadow-lg ring-2 ring-black/30 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="mode === 'still' ? undefined : swapped ? t.proof.media.showMain : t.proof.media.showFront"
      :aria-hidden="mode === 'still' ? 'true' : undefined"
      :class="insetCorner === 'bottom-end' ? 'end-1.5 bottom-1.5' : 'start-2.5 top-2.5'"
      data-testid="proof-inset"
      @click.stop="swapped = !swapped"
    >
      <img
        crossorigin="use-credentials"
        :src="cornerImage"
        alt=""
        decoding="async"
        draggable="false"
        class="size-full object-cover"
      >
    </component>

    <!-- A clip that is not playing says it is one, so nobody mistakes the still for the proof. -->
    <span
      v-if="isVideo && !plays"
      class="pointer-events-none absolute end-2 top-2 flex size-7 items-center justify-center rounded-full bg-black/60 text-white"
      :aria-label="t.proof.media.video"
      role="img"
      data-testid="proof-video-mark"
    >
      <UIcon
        name="i-lucide-play"
        class="size-3.5"
        aria-hidden="true"
      />
    </span>

    <button
      v-if="plays && mode === 'inline'"
      type="button"
      class="absolute end-2.5 bottom-2.5 flex size-11 items-center justify-center rounded-full bg-black/55 text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
      :aria-label="muted ? t.proof.media.soundOn : t.proof.media.soundOff"
      :aria-pressed="!muted"
      data-testid="proof-sound"
      @click.stop="toggleSound"
    >
      <UIcon
        :name="muted ? 'i-lucide-volume-x' : 'i-lucide-volume-2'"
        class="size-5"
        aria-hidden="true"
      />
    </button>
  </div>
</template>
