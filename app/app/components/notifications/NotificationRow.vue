<script setup lang="ts">
import { hapticTap } from '~/utils/haptics'
import type { NotificationLine } from '~/api/types'

/**
 * One line in the bell.
 *
 * Who, and what, in the reader's language — composed from the kind and its
 * parts by the same function the service worker writes a lock screen with, so
 * the two cannot say one event two ways (`notificationText`).
 *
 * The whole row is the link: a line in the bell is there to take somebody to
 * what it is about. What is new is marked with weight and a dot in the text
 * colour. It is a state, so no accent (docs/adr/0015-qdos-design-language.md).
 *
 * Swiping it to the left deletes it, the way a line goes in every mail and
 * message list on a phone. Deleting is final, so what the swipe uncovers is
 * red. A gesture must never be the only way to do something, so the row also
 * carries a delete button that stays out of sight until it has keyboard focus
 * — and "Alle löschen" in the header is the visible way for everybody else.
 * Only a horizontal drag is taken; a vertical one is handed back to the list,
 * as on the swipe stack (ProofSwipeStack).
 */
const props = defineProps<{
  line: NotificationLine
  /** Shared clock, so SSR and the browser agree on "vor 12 Min". */
  now: number
}>()

const emit = defineEmits<{ dismiss: [line: NotificationLine] }>()

const t = useMessages()

const text = computed(() => notificationText(props.line, t.value))
const to = computed(() => notificationLink(props.line))
const icon = computed(() => notificationIcon(props.line))
const time = computed(() => formatRelativeTime(props.line.occurredAt, props.now, t.value))

/** How far the row has to travel before letting go deletes it. */
const THRESHOLD = 96
/** Movement below this has not chosen an axis yet. */
const AXIS_SLOP = 8
const LEAVE_MS = 200

const offset = ref(0)
const isDragging = ref(false)
const isLeaving = ref(false)

let start: { id: number, x: number, y: number } | null = null
let axis: 'x' | 'y' | null = null
let swallowClick = false

const rowStyle = computed(() => ({
  transform: `translateX(${offset.value}px)`,
  transition: isDragging.value ? 'none' : `transform ${LEAVE_MS}ms ease-out`,
}))

/** 0 to 1: how close letting go is to deleting, for the icon underneath. */
const strength = computed(() => Math.min(1, Math.max(0, -offset.value / THRESHOLD)))

function dismiss() {
  if (isLeaving.value) return

  isLeaving.value = true
  offset.value = -(window.innerWidth + 40)
  setTimeout(() => emit('dismiss', props.line), LEAVE_MS)
}

function onPointerDown(event: PointerEvent) {
  swallowClick = false
  if (isLeaving.value || (event.pointerType === 'mouse' && event.button !== 0)) return

  start = { id: event.pointerId, x: event.clientX, y: event.clientY }
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
      start = null
      return
    }

    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId)
    isDragging.value = true
  }

  // Left only; a drag to the right has nothing underneath it.
  offset.value = Math.min(0, dx)
}

function onPointerUp(event: PointerEvent) {
  if (!start || event.pointerId !== start.id) return

  const wasDrag = axis === 'x'

  start = null
  axis = null
  isDragging.value = false

  if (!wasDrag) return

  // The drag ends on the link, and a drag is not a tap on it.
  swallowClick = true

  if (event.type === 'pointerup' && -offset.value > THRESHOLD) {
    hapticTap()
    dismiss()
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
    class="relative overflow-hidden rounded-(--q2-radius-lg)"
    data-testid="notification-line"
  >
    <!-- What the swipe uncovers. Drawn only while there is a swipe, so a row
         at rest has no red edge showing through its rounded corners. -->
    <div
      v-if="offset < 0"
      class="absolute inset-0 flex items-center justify-end bg-(--ui-error) pe-5 text-white"
      aria-hidden="true"
    >
      <UIcon
        name="i-lucide-trash-2"
        class="size-5"
        :style="{ opacity: 0.4 + strength * 0.6, transform: `scale(${0.8 + strength * 0.2})` }"
      />
    </div>

    <div
      class="relative touch-pan-y"
      :class="isDragging ? 'select-none' : ''"
      :style="rowStyle"
      @pointerdown="onPointerDown"
      @pointermove="onPointerMove"
      @pointerup="onPointerUp"
      @pointercancel="onPointerUp"
      @click.capture="onClickCapture"
    >
      <NuxtLink
        :to="to"
        class="q2-card flex items-center gap-3 px-3 py-3 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-(--ui-primary)"
        data-testid="notification-row"
        draggable="false"
      >
        <AppAvatar
          v-if="line.actor"
          :initials="line.actor.initials"
          :color="line.actor.avatarColor"
          :image-id="line.actor.avatarImageId"
          :size="40"
        />

        <!-- Nobody to picture: a verdict, a lifted pause. Anonymous by design,
             so the tile is an icon on a neutral surface rather than a face. -->
        <span
          v-else
          class="flex size-10 shrink-0 items-center justify-center rounded-full bg-(--ui-bg-accented) text-(--ui-text)"
          aria-hidden="true"
        >
          <UIcon
            :name="icon"
            class="size-5"
          />
        </span>

        <div
          class="min-w-0 flex-1"
          data-q2-private
        >
          <!-- A name, and what they did to something of yours — which quotes a
               goal title, so the whole line is private. -->
          <p
            class="text-[13px] leading-snug"
            :class="line.isNew ? 'text-(--ui-text)' : 'text-(--ui-text-muted)'"
          >
            <b
              v-if="text.who"
              class="font-extrabold"
            >{{ text.who }}</b> {{ text.text }}
          </p>
          <p class="mt-0.5 text-[11px] font-semibold text-(--ui-text-dimmed)">
            {{ time }}
          </p>
        </div>

        <span
          v-if="line.isNew"
          class="size-2 shrink-0 rounded-full bg-(--ui-text)"
          data-testid="notification-new"
        >
          <span class="sr-only">{{ t.notify.new }}</span>
        </span>
      </NuxtLink>

      <!-- The swipe's equivalent for a keyboard or a screen reader: out of
           sight until it has focus, then laid over the end of the row. -->
      <button
        v-if="line.id"
        type="button"
        class="sr-only focus:not-sr-only focus:absolute focus:inset-y-2 focus:end-2 focus:flex focus:items-center focus:rounded-full focus:bg-(--ui-error) focus:px-3 focus:text-[12px] focus:font-bold focus:text-white"
        data-testid="notification-dismiss"
        @click="dismiss"
      >
        {{ t.notify.dismiss }}
      </button>
    </div>
  </div>
</template>
