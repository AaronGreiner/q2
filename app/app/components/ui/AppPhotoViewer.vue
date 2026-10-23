<script setup lang="ts">
import { proofMediaOf } from '~/utils/proofMedia'

/**
 * A photograph, full screen: pinch to zoom, drag down to close.
 *
 * Mounted once in the layout and driven by `usePhotoViewer`, so every picture
 * in the app opens into the same view — a friend's proof, a challenge
 * contribution, an avatar.
 *
 * **The gestures are drawn here rather than left to the browser.** q2 turns
 * pinch-zoom off for the whole document so that an installed app does not
 * zoom out of its own layout (docs/adr/0013-app-like-input.md). A photograph
 * is the one thing that has to be looked at closely — a proof is judged on
 * detail — so this surface takes every touch itself (`touch-action: none`)
 * and zooms only the picture, never the page.
 *
 * What the picture is *of* travels with it. A photograph without its goal or
 * its challenge prompt is unplaceable, and full screen is exactly where the
 * card around it has gone; the bar at the bottom gives that back, and steps
 * aside while somebody is zoomed in on detail.
 *
 * A proof can be more than one picture (#124, #125, #151), and is drawn by
 * `ProofMedia` like everywhere else: a clip plays here with the player's own
 * controls, starting without sound, and takes no gestures — they would be the
 * player's. The front camera's picture or clip sits in a top corner, can be
 * dragged to the other one, and a tap makes it the large one — a photograph
 * zoomable like any other — and puts the main one in the corner instead.
 */
const t = useMessages()
const { current, close } = usePhotoViewer()

/** The front camera's part is the large one. */
const swapped = ref(false)

const parts = computed(() => (current.value ? proofMediaOf(current.value) : null))

/** A clip is playing large, rather than a photograph being drawn there. */
const playsClip = computed(() => {
  const shown = parts.value
  if (!shown) return false
  return (swapped.value && shown.insetImageId ? shown.insetMedia : shown.media) === 'Video'
})

/** Below the status bar, and in the end corner below the close button too. */
const insetTop = 'max(0.75rem, env(safe-area-inset-top))'
const insetTopEnd = 'calc(max(0.75rem, env(safe-area-inset-top)) + 3.5rem)'

const MIN_SCALE = 1
const MAX_SCALE = 4
const DOUBLE_TAP_SCALE = 2.5
/** How far a drag down has to travel before letting go closes the view. */
const DISMISS_DISTANCE = 120
/** Movement below this is a tap, not a drag. */
const TAP_SLOP = 8

const scale = ref(1)
const offsetX = ref(0)
const offsetY = ref(0)
const isTracking = ref(false)

const closeButton = useTemplateRef<HTMLButtonElement>('closeButton')
let returnFocusTo: HTMLElement | null = null

const isZoomed = computed(() => scale.value > 1.01)

/** Dragging down fades the black away, so the screen underneath shows what letting go will do. */
const backdropOpacity = computed(() =>
  (isZoomed.value ? 1 : 1 - Math.min(0.6, Math.abs(offsetY.value) / 400)))

const imageStyle = computed(() => ({
  transform: `translate3d(${offsetX.value}px, ${offsetY.value}px, 0) scale(${scale.value})`,
  transition: isTracking.value ? 'none' : 'transform 200ms ease-out',
}))

function reset() {
  scale.value = 1
  offsetX.value = 0
  offsetY.value = 0
}

// Leaving the screen leaves its photograph behind too — a notification tapped
// while one is open must not land under it.
const route = useRoute()
watch(() => route.fullPath, () => close())

watch(current, async (item, previous) => {
  reset()
  swapped.value = false

  if (item && !previous) {
    returnFocusTo = document.activeElement instanceof HTMLElement ? document.activeElement : null
    await nextTick()
    closeButton.value?.focus()
  }
  else if (!item && previous) {
    // Back to the card it was opened from, not to the top of the page.
    returnFocusTo?.focus()
    returnFocusTo = null
  }
})

/*
 * The gesture, as a small state machine over the pointers currently down.
 *
 * Two pointers pinch; one pointer pans a zoomed picture or, at normal size,
 * drags the whole view down to close it. Pointer events rather than touch
 * events, so a mouse in the development browser drives the same code a
 * finger does.
 */
const pointers = new Map<number, { x: number, y: number }>()

type Gesture
  = | { kind: 'pinch', distance: number, scale: number, midX: number, midY: number, x: number, y: number }
    | { kind: 'drag', startX: number, startY: number, x: number, y: number, moved: boolean }

let gesture: Gesture | null = null
let lastTapAt = 0

function distance(a: { x: number, y: number }, b: { x: number, y: number }) {
  return Math.hypot(a.x - b.x, a.y - b.y)
}

function beginFromPointers() {
  const [a, b] = [...pointers.values()]

  if (a && b) {
    gesture = {
      kind: 'pinch',
      distance: Math.max(1, distance(a, b)),
      scale: scale.value,
      midX: (a.x + b.x) / 2,
      midY: (a.y + b.y) / 2,
      x: offsetX.value,
      y: offsetY.value,
    }
  }
  else if (a) {
    gesture = { kind: 'drag', startX: a.x, startY: a.y, x: offsetX.value, y: offsetY.value, moved: false }
  }
  else {
    gesture = null
  }
}

// Whichever is large now starts at normal size.
watch(swapped, () => reset())

function onPointerDown(event: PointerEvent) {
  // A playing clip's gestures are its player's.
  if (playsClip.value) return

  const surface = event.currentTarget as HTMLElement
  surface.setPointerCapture(event.pointerId)
  pointers.set(event.pointerId, { x: event.clientX, y: event.clientY })
  isTracking.value = true
  beginFromPointers()
}

function onPointerMove(event: PointerEvent) {
  if (!pointers.has(event.pointerId) || !gesture) return

  pointers.set(event.pointerId, { x: event.clientX, y: event.clientY })

  if (gesture.kind === 'pinch') {
    const [a, b] = [...pointers.values()]
    if (!a || !b) return

    scale.value = Math.min(MAX_SCALE, Math.max(MIN_SCALE, gesture.scale * (distance(a, b) / gesture.distance)))
    offsetX.value = gesture.x + ((a.x + b.x) / 2 - gesture.midX)
    offsetY.value = gesture.y + ((a.y + b.y) / 2 - gesture.midY)
    return
  }

  const dx = event.clientX - gesture.startX
  const dy = event.clientY - gesture.startY

  if (Math.hypot(dx, dy) > TAP_SLOP) gesture.moved = true

  if (isZoomed.value) {
    offsetX.value = gesture.x + dx
    offsetY.value = gesture.y + dy
  }
  else {
    // At normal size the only way to move is down and out.
    offsetY.value = dy
  }
}

function onPointerUp(event: PointerEvent) {
  if (!pointers.has(event.pointerId)) return

  const ended = gesture
  pointers.delete(event.pointerId)

  if (pointers.size > 0) {
    // One finger lifted off a pinch: carry on as a pan with the other.
    beginFromPointers()
    return
  }

  isTracking.value = false
  gesture = null

  if (ended?.kind === 'drag' && !ended.moved && event.type === 'pointerup') {
    onTap()
    return
  }

  if (!isZoomed.value) {
    if (Math.abs(offsetY.value) > DISMISS_DISTANCE) {
      close()
      return
    }

    reset()
  }
}

/** A double tap zooms in on the picture, or back out. */
function onTap() {
  const now = Date.now()

  if (now - lastTapAt < 300) {
    lastTapAt = 0

    if (isZoomed.value) reset()
    else scale.value = DOUBLE_TAP_SCALE
    return
  }

  lastTapAt = now
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    event.preventDefault()
    close()
  }
  else if (event.key === 'Tab') {
    // Focus goes round the controls on this view — the close button, the
    // corner picture, a clip's player — and never wanders onto the screen
    // hidden behind it.
    event.preventDefault()

    const controls = [...(event.currentTarget as HTMLElement).querySelectorAll<HTMLElement>('button, video[controls]')]
    const at = controls.indexOf(document.activeElement as HTMLElement)
    const next = controls[(at + (event.shiftKey ? controls.length - 1 : 1)) % controls.length] ?? closeButton.value

    next?.focus()
  }
}
</script>

<template>
  <Transition
    enter-active-class="transition-opacity duration-150"
    leave-active-class="transition-opacity duration-150"
    enter-from-class="opacity-0"
    leave-to-class="opacity-0"
  >
    <div
      v-if="current"
      class="fixed inset-0 z-50 flex flex-col text-white"
      role="dialog"
      aria-modal="true"
      :aria-label="current.title ?? t.viewer.label"
      data-testid="photo-viewer"
      @keydown="onKeydown"
    >
      <div
        class="absolute inset-0 bg-black"
        :style="{ opacity: backdropOpacity }"
        aria-hidden="true"
      />

      <!-- Somebody's photograph, at its largest: replaced as a whole in Session
           Replay, like every other element that draws one (#126). -->
      <div
        class="relative min-h-0 flex-1 overflow-hidden select-none"
        :class="playsClip ? 'pb-28' : 'touch-none'"
        data-q2-block
        @pointerdown="onPointerDown"
        @pointermove="onPointerMove"
        @pointerup="onPointerUp"
        @pointercancel="onPointerUp"
      >
        <!-- Sound off until somebody turns it on with the player (#124). -->
        <ProofMedia
          v-if="parts"
          v-model:swapped="swapped"
          :parts="parts"
          fit="contain"
          mode="controls"
          :alt="current.title ?? t.viewer.label"
          :inset-top="insetTop"
          :inset-top-end="insetTopEnd"
          :zoom-style="imageStyle"
        />
      </div>

      <button
        ref="closeButton"
        type="button"
        class="absolute end-3 top-[max(0.75rem,env(safe-area-inset-top))] flex size-11 items-center justify-center rounded-full bg-black/50 text-white backdrop-blur-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
        :aria-label="t.viewer.close"
        data-testid="photo-viewer-close"
        @click="close()"
      >
        <UIcon
          name="i-lucide-x"
          class="size-6"
          aria-hidden="true"
        />
      </button>

      <!--
        Out of the way while zoomed: somebody looking at the corner of a
        picture wants the corner, not a caption over it.
      -->
      <div
        v-if="current.title || current.subtitle || current.meta || current.source"
        class="pointer-events-none absolute inset-x-0 bottom-0 bg-linear-to-t from-black/85 to-transparent px-5 pt-12 pb-[max(1.25rem,var(--q2-safe-bottom))] transition-opacity duration-150"
        :class="isZoomed ? 'opacity-0' : 'opacity-100'"
        data-testid="photo-viewer-info"
      >
        <p
          v-if="current.title"
          class="text-base font-extrabold leading-snug"
          data-q2-private
        >
          {{ current.title }}
        </p>
        <p
          v-if="current.subtitle || current.meta"
          class="mt-1 text-[13px] font-semibold text-white/70"
          data-q2-private
        >
          {{ [current.subtitle, current.meta].filter(Boolean).join(' · ') }}
        </p>
        <ProofSourceBadge
          v-if="current.source"
          :source="current.source"
          class="mt-2"
        />
      </div>
    </div>
  </Transition>
</template>
