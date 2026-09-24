<script setup lang="ts">
import type { FeedProof, ProofVoteValue } from '~/api/types'

/**
 * The photographs waiting for your verdict, as a stack you swipe through.
 *
 * Right believes it, left doubts it — the direction people already know from
 * every other app that asks one yes-or-no question per picture. It is still
 * one card at a time and still no "skip": the gesture changes how a verdict is
 * given, not whether one is (see `pages/vote.vue`).
 *
 * The buttons on `ProofCard` stay. A swipe is invisible to a screen reader and
 * awkward for anybody who cannot drag, and a verdict must never depend on a
 * gesture alone. Pressing one sends the card off the same way a swipe would,
 * so the two paths look like one.
 *
 * Only a horizontal drag is taken. The first few pixels decide the axis, and a
 * vertical one is handed back to the page, so the start screen still scrolls
 * with a thumb that happens to start on a photograph.
 */
const props = withDefaults(defineProps<{
  cards: readonly FeedProof[]
  busy?: boolean
}>(), {
  busy: false,
})

const emit = defineEmits<{ vote: [id: string, value: ProofVoteValue] }>()

const t = useMessages()

/** How far a card has to travel before letting go decides it. */
const THRESHOLD = 96
/** A flick decides it too, if it is fast enough and clearly meant. */
const FLICK_VELOCITY = 0.6
const FLICK_MIN_DISTANCE = 40
/** Movement below this has not chosen an axis yet. */
const AXIS_SLOP = 8
const LEAVE_MS = 220

const current = computed(() => props.cards[0] ?? null)

const offset = ref(0)
const isDragging = ref(false)
const leaving = ref<ProofVoteValue | null>(null)

let start: { id: number, x: number, y: number, at: number } | null = null
let axis: 'x' | 'y' | null = null
let swallowClick = false
let leaveTimer: ReturnType<typeof setTimeout> | null = null

const cardStyle = computed(() => ({
  transform: `translateX(${offset.value}px) rotate(${offset.value / 18}deg)`,
  transition: isDragging.value ? 'none' : `transform ${LEAVE_MS}ms ease-out`,
}))

/** 0 to 1: how close the card is to being decided, for the stamp on it. */
const confirmStrength = computed(() => Math.min(1, Math.max(0, offset.value / THRESHOLD)))
const doubtStrength = computed(() => Math.min(1, Math.max(0, -offset.value / THRESHOLD)))

function settle() {
  if (leaveTimer) clearTimeout(leaveTimer)
  leaveTimer = null
  leaving.value = null
  offset.value = 0
}

// A new card starts in the middle. A failed vote leaves the same card on top
// once the queue has been read again, and it comes back rather than staying
// off screen.
watch(() => current.value?.proof.id, settle)
watch(() => props.busy, (busy) => {
  if (!busy && !leaveTimer) settle()
})

onBeforeUnmount(() => {
  if (leaveTimer) clearTimeout(leaveTimer)
})

/** Sends the card off in the direction of the verdict, then reports it. */
function decide(value: ProofVoteValue) {
  const card = current.value
  if (!card || leaving.value || props.busy) return

  leaving.value = value
  offset.value = (value === 'Confirm' ? 1 : -1) * (window.innerWidth + 120)

  leaveTimer = setTimeout(() => {
    leaveTimer = null
    emit('vote', card.proof.id, value)
  }, LEAVE_MS)
}

function onPointerDown(event: PointerEvent) {
  swallowClick = false
  if (leaving.value || props.busy || (event.pointerType === 'mouse' && event.button !== 0)) return

  start = { id: event.pointerId, x: event.clientX, y: event.clientY, at: event.timeStamp }
  axis = null
}

function onPointerMove(event: PointerEvent) {
  if (!start || event.pointerId !== start.id) return

  const dx = event.clientX - start.x
  const dy = event.clientY - start.y

  if (!axis) {
    if (Math.hypot(dx, dy) < AXIS_SLOP) return

    axis = Math.abs(dx) > Math.abs(dy) ? 'x' : 'y'

    if (axis === 'y') {
      // A scroll, not a verdict. The browser has it from here.
      start = null
      return
    }

    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId)
    isDragging.value = true
  }

  offset.value = dx
}

function onPointerUp(event: PointerEvent) {
  if (!start || event.pointerId !== start.id) return

  const wasDrag = axis === 'x'
  const elapsed = Math.max(1, event.timeStamp - start.at)
  const velocity = offset.value / elapsed

  start = null
  axis = null
  isDragging.value = false

  if (!wasDrag) return

  // The drag ends on whatever is under the finger, which is often the photo
  // or a button. It was a drag, so that tap must not happen as well.
  swallowClick = true

  const distance = Math.abs(offset.value)

  if (event.type === 'pointerup'
    && (distance > THRESHOLD || (Math.abs(velocity) > FLICK_VELOCITY && distance > FLICK_MIN_DISTANCE))) {
    decide(offset.value > 0 ? 'Confirm' : 'Doubt')
  }
  else {
    offset.value = 0
  }
}

function onClickCapture(event: MouseEvent) {
  if (!swallowClick) return

  swallowClick = false
  event.preventDefault()
  event.stopPropagation()
}
</script>

<template>
  <div
    v-if="current"
    class="flex flex-col gap-2.5"
    data-testid="proof-swipe-stack"
  >
    <div class="relative pb-2">
      <!-- The edge of the next card, so it is clear there is more to come. -->
      <div
        v-if="cards.length > 1"
        class="q2-card absolute inset-x-3 top-3 bottom-0 opacity-50"
        aria-hidden="true"
      />

      <div
        class="relative touch-pan-y"
        :class="isDragging ? 'select-none' : ''"
        :style="cardStyle"
        data-testid="proof-swipe-card"
        @pointerdown="onPointerDown"
        @pointermove="onPointerMove"
        @pointerup="onPointerUp"
        @pointercancel="onPointerUp"
        @click.capture="onClickCapture"
      >
        <!--
          Keyed on the proof, so Vue replaces the card rather than repainting
          the one that is there. Without it the next photograph would fade in
          behind the buttons somebody's thumb is still on.
        -->
        <ProofCard
          :key="current.proof.id"
          :proof="current.proof"
          :goal-title="current.goalTitle"
          :busy="busy || leaving !== null"
          @vote="decide"
        />

        <!--
          The verdict the card will get if it is let go now. Decorative: the
          buttons carry the same choice with a name, and the result is
          announced by the next card arriving.
        -->
        <span
          class="pointer-events-none absolute start-4 top-16 flex -rotate-12 items-center gap-1.5 rounded-full bg-(--q2-accent-solid) px-3.5 py-2 text-[13px] font-extrabold text-(--q2-accent-contrast) shadow-lg"
          :style="{ opacity: confirmStrength }"
          aria-hidden="true"
        >
          <UIcon
            name="i-lucide-check"
            class="size-4"
          />
          {{ t.vote.confirm }}
        </span>

        <span
          class="pointer-events-none absolute end-4 top-16 flex rotate-12 items-center gap-1.5 rounded-full bg-(--ui-text) px-3.5 py-2 text-[13px] font-extrabold text-(--ui-bg) shadow-lg"
          :style="{ opacity: doubtStrength }"
          aria-hidden="true"
        >
          <UIcon
            name="i-lucide-circle-help"
            class="size-4"
          />
          {{ t.vote.doubt }}
        </span>
      </div>
    </div>

    <p class="text-center text-[11px] font-semibold text-(--ui-text-dimmed)">
      {{ t.vote.swipeHint }}
    </p>
  </div>
</template>
