<script setup lang="ts">
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
 */
const t = useMessages()
const { current, close } = usePhotoViewer()
const picture = useImageSource(() => current.value?.imageId)

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
const failed = ref(false)

const closeButton = useTemplateRef<HTMLButtonElement>('closeButton')
let returnFocusTo: HTMLElement | null = null

const source = computed(() =>
  (current.value && !failed.value ? picture.value : null))

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
  failed.value = false

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

function onPointerDown(event: PointerEvent) {
  (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId)
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
    // The close button is the only control, so focus stays on it rather than
    // wandering onto the screen hidden behind the picture.
    event.preventDefault()
    closeButton.value?.focus()
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

      <div
        class="relative min-h-0 flex-1 touch-none overflow-hidden select-none"
        @pointerdown="onPointerDown"
        @pointermove="onPointerMove"
        @pointerup="onPointerUp"
        @pointercancel="onPointerUp"
      >
        <img
          v-if="source"
          crossorigin="use-credentials"
          :src="source"
          :alt="current.title ?? t.viewer.label"
          decoding="async"
          draggable="false"
          class="size-full object-contain will-change-transform"
          :style="imageStyle"
          data-testid="photo-viewer-image"
          @error="failed = true"
        >

        <div
          v-else
          class="flex size-full items-center justify-center"
        >
          <UIcon
            name="i-lucide-image-off"
            class="size-10 text-white/50"
            aria-hidden="true"
          />
        </div>
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
        v-if="current.title || current.subtitle || current.meta"
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
      </div>
    </div>
  </Transition>
</template>
