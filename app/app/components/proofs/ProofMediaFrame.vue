<script setup lang="ts">
import type { StyleValue } from 'vue'
import type { MediaKind } from '~/api/types'
import { keepInStep } from '~/utils/clipsInStep'

/**
 * What a proof shows, drawn from addresses: a photograph or a clip, and the
 * front camera's picture or clip in a corner (#124, #125, #151).
 *
 * The one place that knows how a proof is laid out, so the card, the full
 * screen, the gallery and the camera's own preview cannot disagree about it.
 * It is handed addresses rather than ids — `ProofMedia` resolves stored ones,
 * the camera passes the `blob:` addresses of what it is still holding.
 *
 * **Two layers, one large and one in the corner.** The main part and the front
 * camera's part each keep their own element and only trade places, so a clip
 * that is swapped into the corner carries on from where it was rather than
 * loading again. The rear camera is always the large one first.
 *
 * **The corner swaps on a tap and moves on a drag** — along the top, landing
 * in the nearer corner, so whatever it covers can be seen (#151,
 * `useInsetCorner`). Where the frame is itself a button (a tile), the corner is
 * only shown, never pressed, and stays in its own corner.
 *
 * **Two clips play as one.** Where both cameras filmed at once, the corner
 * plays too, kept on the large one's moment (`utils/clipsInStep.ts`): the large
 * clip leads, carries the controls and the sound, and the corner is always
 * silent. A clip beside a front photograph also keeps playing in the corner
 * when the photograph is swapped in.
 *
 * **A clip plays without sound until somebody asks for it** (#124). On a card
 * it plays while it is on screen and pauses when it scrolls away, with one
 * button for the sound; full screen it has the player's own controls; on a
 * tile it does not play at all and shows its still with a play mark.
 *
 * **Everything here is somebody's face or room**, so the whole frame is
 * replaced in Session Replay (`data-q2-block`, ADRs 0005 and 0017) — the clips,
 * the stills and the front picture alike.
 */
const props = withDefaults(defineProps<{
  kind: MediaKind
  /** The photograph or the clip. */
  src: string | null
  /** A clip's still. */
  posterSrc?: string | null
  /** Whether the front camera's part is a photograph or a clip. */
  insetKind?: MediaKind
  /** The front camera's picture or clip. */
  insetSrc?: string | null
  /** A front clip's still. */
  insetPosterSrc?: string | null
  /** `contain` where the whole picture matters — a screenshot, the full screen. */
  fit?: 'cover' | 'contain'
  /** `inline` on a card, `controls` full screen, `still` on a tile. */
  mode?: 'inline' | 'controls' | 'still'
  /** Whether the large picture opens something — the card's full-screen view. */
  openable?: boolean
  /** The accessible name of that button. */
  openLabel?: string
  alt?: string
  /** Where the front picture sits: along the top, or fixed bottom-end on a tile that keeps its top for its status. */
  insetCorner?: 'top-start' | 'bottom-end'
  /** How far down the corner sits, where the frame runs under something — the full screen's status bar. */
  insetTop?: string
  /** The same in the end corner, where the full screen's close button is. */
  insetTopEnd?: string
  /** Applied to a large photograph: the full screen's pinch and pan. */
  zoomStyle?: StyleValue
}>(), {
  posterSrc: null,
  insetKind: 'Photo',
  insetSrc: null,
  insetPosterSrc: null,
  fit: 'cover',
  mode: 'inline',
  openable: false,
  openLabel: undefined,
  alt: '',
  insetCorner: 'top-start',
  insetTop: '0.625rem',
  insetTopEnd: undefined,
  zoomStyle: undefined,
})

const emit = defineEmits<{ open: [] }>()

/** The front camera's part is the large one. A model, so the full screen can tell whether a clip or a photograph is large. */
const swapped = defineModel<boolean>('swapped', { default: false })

const t = useMessages()
const corner = useInsetCorner()

const muted = ref(true)
const mainFailed = ref(false)
const insetFailed = ref(false)
const visible = ref(false)

const root = useTemplateRef<HTMLElement>('root')
const mainPlayer = useTemplateRef<HTMLVideoElement>('mainPlayer')
const insetPlayer = useTemplateRef<HTMLVideoElement>('insetPlayer')

watch(() => [props.src, props.insetSrc], () => {
  swapped.value = false
  mainFailed.value = false
  insetFailed.value = false
  corner.reset()
})

const isVideo = computed(() => props.kind === 'Video')
const insetIsVideo = computed(() => props.insetKind === 'Video')

/** The stills that stand for each part where a clip cannot play. */
const mainStill = computed(() => (isVideo.value ? props.posterSrc : props.src))
const insetStill = computed(() => (insetIsVideo.value ? props.insetPosterSrc : props.insetSrc))

const hasInset = computed(() => !!(props.insetSrc || props.insetPosterSrc))

/** A clip plays wherever it is, large or in the corner — except on a tile. */
const mainPlays = computed(() => isVideo.value && props.mode !== 'still' && !!props.src && !mainFailed.value)
const insetPlays = computed(() => insetIsVideo.value && props.mode !== 'still' && !!props.insetSrc && !insetFailed.value)

const largePlays = computed(() => (swapped.value ? insetPlays.value : mainPlays.value))

/** The clip the other one follows: the large one where it is a clip, else the only one there is. */
const mainLeads = computed(() => mainPlays.value && (!swapped.value || !insetPlays.value))
const insetLeads = computed(() => insetPlays.value && !mainLeads.value)

const interactive = computed(() => props.mode !== 'still')
const movable = computed(() => interactive.value && props.insetCorner === 'top-start')

const fitClass = computed(() => (props.fit === 'contain' ? 'object-contain' : 'object-cover'))

const largeLayer = 'absolute inset-0'

const cornerPlace = computed(() => {
  if (props.insetCorner === 'bottom-end') return 'end-1.5 bottom-1.5'
  return corner.side.value === 'end' ? 'end-2.5' : 'start-2.5'
})

const cornerGeometry = computed(() => ['absolute aspect-[3/4] w-[28%] max-w-32 rounded-(--q2-radius-sm)', cornerPlace.value])

const cornerStyle = computed<StyleValue>(() => (props.insetCorner === 'bottom-end'
  ? {}
  : [{ top: corner.side.value === 'end' ? props.insetTopEnd ?? props.insetTop : props.insetTop }, corner.style.value]))

const cornerLayer = computed(() => [
  ...cornerGeometry.value,
  'z-[1] overflow-hidden bg-(--ui-bg-elevated) shadow-lg ring-2 ring-black/30',
])

function layerOf(large: boolean) {
  return large ? { class: largeLayer, style: undefined } : { class: cornerLayer.value, style: cornerStyle.value }
}

const mainLayer = computed(() => layerOf(!swapped.value))
const insetLayer = computed(() => layerOf(swapped.value))

/*
 * The corner clip follows the leading one for as long as both are there. A
 * swap changes which leads, so the pairing is made again.
 */
watch(
  () => [mainLeads.value ? mainPlayer.value : insetPlayer.value, mainLeads.value ? insetPlayer.value : mainPlayer.value] as const,
  ([leader, follower], _, onCleanup) => {
    if (leader && follower) onCleanup(keepInStep(leader, follower))
  },
  { flush: 'post' },
)

/*
 * On a card a clip plays while at least half of it is on screen. Not
 * `autoplay`: a conversation can hold a week of clips, and every one of them
 * playing — and, in the browser, loading — at once is a phone getting warm.
 */
let observer: IntersectionObserver | null = null

onMounted(() => {
  if (props.mode !== 'inline' || typeof IntersectionObserver === 'undefined' || !root.value) return

  observer = new IntersectionObserver(([entry]) => {
    visible.value = !!entry?.isIntersecting
  }, { threshold: 0.5 })

  observer.observe(root.value)
})

onBeforeUnmount(() => observer?.disconnect())

watch(
  () => [visible.value, mainLeads.value ? mainPlayer.value : insetLeads.value ? insetPlayer.value : null] as const,
  ([isVisible, leader]) => {
    if (!leader || props.mode !== 'inline') return

    if (isVisible) leader.play().catch(() => {})
    else leader.pause()
  },
  { flush: 'post' },
)

function toggleSound() {
  muted.value = !muted.value

  // Asking for the sound is asking for the clip, even one scrolled half away.
  const large = swapped.value ? insetPlayer.value : mainPlayer.value
  if (!muted.value) large?.play().catch(() => {})
}

/** The player's own mute button, full screen: what it says carries over to the other clip when they swap. */
function heardFrom(large: boolean, event: Event) {
  if (large) muted.value = (event.target as HTMLVideoElement).muted
}

function onCornerClick() {
  if (corner.tookClick()) return

  swapped.value = !swapped.value
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
    <!--
      The main part. `playsinline` keeps iOS from taking a clip full screen on
      its own; `crossorigin` before `:src`, as for every picture
      (api/images.ts).
    -->
    <div
      :class="mainLayer.class"
      :style="mainLayer.style"
    >
      <video
        v-if="mainPlays"
        ref="mainPlayer"
        crossorigin="use-credentials"
        :src="src ?? undefined"
        :poster="posterSrc ?? undefined"
        playsinline
        :loop="mainLeads"
        preload="metadata"
        :muted="swapped || muted"
        :autoplay="mode === 'controls' && mainLeads"
        :controls="mode === 'controls' && !swapped"
        class="size-full"
        :class="swapped ? 'object-cover' : fitClass"
        :data-testid="swapped ? 'proof-inset-video' : 'proof-video'"
        @error="mainFailed = true"
        @volumechange="heardFrom(!swapped, $event)"
      />

      <img
        v-else-if="mainStill && !mainFailed"
        crossorigin="use-credentials"
        :src="mainStill"
        :alt="swapped ? '' : alt"
        decoding="async"
        draggable="false"
        class="size-full"
        :class="swapped ? 'object-cover' : [fitClass, zoomStyle ? 'will-change-transform' : '']"
        :style="swapped ? undefined : zoomStyle"
        :data-testid="swapped ? 'proof-inset-image' : 'proof-image'"
        @error="mainFailed = true"
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
        <span class="sr-only">{{ t.proof.media.unavailable }}</span>
      </span>
    </div>

    <!-- The front camera's part: in the corner the thumb does not cover, until it is swapped in. -->
    <div
      v-if="hasInset"
      :class="insetLayer.class"
      :style="insetLayer.style"
      :aria-hidden="mode === 'still' ? 'true' : undefined"
      :data-testid="mode === 'still' ? 'proof-inset' : undefined"
    >
      <video
        v-if="insetPlays"
        ref="insetPlayer"
        crossorigin="use-credentials"
        :src="insetSrc ?? undefined"
        :poster="insetPosterSrc ?? undefined"
        playsinline
        :loop="insetLeads"
        preload="metadata"
        :muted="!swapped || muted"
        :autoplay="mode === 'controls' && insetLeads"
        :controls="mode === 'controls' && swapped"
        class="size-full"
        :class="swapped ? fitClass : 'object-cover'"
        :data-testid="swapped ? 'proof-video' : 'proof-inset-video'"
        @error="insetFailed = true"
        @volumechange="heardFrom(swapped, $event)"
      />

      <img
        v-else-if="insetStill && !insetFailed"
        crossorigin="use-credentials"
        :src="insetStill"
        :alt="swapped ? alt : ''"
        decoding="async"
        draggable="false"
        class="size-full"
        :class="swapped ? [fitClass, zoomStyle ? 'will-change-transform' : ''] : 'object-cover'"
        :style="swapped ? zoomStyle : undefined"
        :data-testid="swapped ? 'proof-image' : 'proof-inset-image'"
        @error="insetFailed = true"
      >
    </div>

    <!-- The card's way to its full screen, under the corner so the corner stays its own. -->
    <button
      v-if="openable"
      type="button"
      class="absolute inset-0 block focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="openLabel"
      data-testid="proof-enlarge"
      @click="emit('open')"
    />

    <!--
      What a finger or a keyboard presses to swap the two, or drags to move the
      corner: laid over it rather than being it, because the picture under it
      changes places with the large one and must not be made again to do so.
    -->
    <button
      v-if="hasInset && interactive"
      type="button"
      class="z-[2] block touch-pan-y focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :class="cornerGeometry"
      :style="cornerStyle"
      :aria-label="swapped ? t.proof.media.showMain : t.proof.media.showFront"
      :aria-keyshortcuts="movable ? 'ArrowLeft ArrowRight' : undefined"
      data-testid="proof-inset"
      :data-side="corner.side.value"
      @pointerdown="movable && corner.onPointerdown($event)"
      @pointermove="movable && corner.onPointermove($event)"
      @pointerup="movable && corner.onPointerup($event)"
      @pointercancel="movable && corner.onPointerup($event)"
      @keydown="movable && corner.onKeydown($event)"
      @click.stop="onCornerClick"
    />

    <!-- A clip that is not playing says it is one, so nobody mistakes the still for the proof. -->
    <span
      v-if="isVideo && !mainPlays"
      class="pointer-events-none absolute end-2 top-2 z-[2] flex size-7 items-center justify-center rounded-full bg-black/60 text-white"
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
      v-if="largePlays && mode === 'inline'"
      type="button"
      class="absolute end-2.5 bottom-2.5 z-[2] flex size-11 items-center justify-center rounded-full bg-black/55 text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
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
