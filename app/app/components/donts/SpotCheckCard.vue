<script setup lang="ts">
import type { Goal } from '~/api/types'

/**
 * An open spot check: three minutes to show, live, what you are doing right
 * now (docs/adr/0038-spot-checks.md).
 *
 * The first thing on the start screen while it is open, and also on the
 * Don't's own screen, because push is off by default: whoever opens the app in
 * those three minutes has to find it without having been rung.
 *
 * The countdown runs in seconds. The shared clock (`useNow`) moves once a
 * minute, which is right for "vor 12 Min" and useless here, so this keeps its
 * own once it is mounted — and starts from the shared one, so the server and
 * the browser render the same first frame.
 *
 * The camera is the one accent: it is the thing to do, now.
 */
const props = defineProps<{
  goal: Goal
  /** The shared clock, for the first frame. */
  now: number
  busy?: boolean
}>()

const emit = defineEmits<{ answer: [goalId: string, checkId: string], expired: [] }>()

const t = useMessages()

const clock = ref(props.now)
let timer: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  clock.value = Date.now()
  timer = setInterval(() => {
    clock.value = Date.now()
  }, 1000)
})

onBeforeUnmount(() => {
  if (timer) clearInterval(timer)
})

const secondsLeft = computed(() => {
  const expires = props.goal.spotCheck ? Date.parse(props.goal.spotCheck.expiresAt) : Number.NaN
  return Number.isNaN(expires) ? 0 : Math.max(0, Math.ceil((expires - clock.value) / 1000))
})

const isOver = computed(() => secondsLeft.value === 0)

// Once, when the three minutes run out, so the screen can read again and the
// card goes — the server has decided it by then.
watch(isOver, (over) => {
  if (over) emit('expired')
})
</script>

<template>
  <section
    v-if="goal.spotCheck"
    class="q2-card p-4"
    aria-labelledby="spot-check-heading"
    data-testid="spot-check"
  >
    <div class="flex items-center justify-between gap-3">
      <h2
        id="spot-check-heading"
        class="q2-eyebrow flex items-center gap-1.5"
      >
        <UIcon
          name="i-lucide-camera"
          class="size-3.5"
          aria-hidden="true"
        />
        {{ t.spotCheck.heading }}
      </h2>

      <!-- A timer, so a screen reader is told it changes without being read
           every second of it. -->
      <span
        role="timer"
        aria-live="off"
        class="text-[13px] font-extrabold tabular-nums"
        data-testid="spot-check-left"
      >{{ isOver ? t.spotCheck.over : t.spotCheck.left(Math.floor(secondsLeft / 60), secondsLeft % 60) }}</span>
    </div>

    <p
      class="mt-2 text-base font-extrabold"
      data-q2-private
    >
      {{ goal.title }}
    </p>
    <p class="mt-0.5 text-[13px] font-semibold text-(--ui-text-muted)">
      {{ t.spotCheck.prompt }}
    </p>

    <UButton
      class="mt-3 w-full justify-center"
      size="xl"
      icon="i-lucide-camera"
      :label="t.spotCheck.answer"
      :aria-label="t.spotCheck.answerFor(goal.title)"
      :disabled="isOver || busy"
      data-testid="spot-check-answer"
      @click="emit('answer', goal.id, goal.spotCheck.id)"
    />
  </section>
</template>
