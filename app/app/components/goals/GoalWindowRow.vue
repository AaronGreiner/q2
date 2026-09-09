<script setup lang="ts">
import type { Goal } from '~/api/types'

/**
 * One goal that is due, with the control that delivers into its window.
 *
 * The same row on the start screen and on the goals screen — which is the
 * point: delivering has to feel identical wherever it happens, including what
 * a screen reader says about it.
 *
 * **The control is a camera, not a tick box.** That is the whole change of
 * stage 4: a window is no longer closed by its owner saying "done", it is
 * closed by other people believing a photograph. So the row has three states
 * rather than two, and the middle one is the new one — *wird geprüft*, which is
 * neither done nor still to do.
 */
const props = withDefaults(defineProps<{
  goal: Goal
  busy?: boolean
}>(), {
  busy: false,
})

const emit = defineEmits<{ deliver: [id: string] }>()

const t = useMessages()

const currentWindow = computed(() => props.goal.current)
const reminder = computed(() => formatClock(props.goal.reminderAt))

const isDelivered = computed(() => !currentWindow.value || currentWindow.value.remainingProofs === 0)
const isWaiting = computed(() => Boolean(currentWindow.value?.pendingProofId))
const canDeliver = computed(() => Boolean(currentWindow.value?.acceptsProof) && !props.busy)
</script>

<template>
  <div
    class="q2-card flex items-center gap-3 px-3.5 py-3"
    data-testid="window-row"
    data-q2-block
  >
    <!--
      A real button, and only a button when there is something to press. The
      accent is spent here and nowhere else in the row, because this is the one
      thing the person can do right now (docs/adr/0015).

      While a photograph is being looked at there is nothing to press: the
      camera would offer a second delivery the server would refuse. The circle
      stays, so the row does not change shape between states — what changes is
      what is in it.

      The `after` box is what a thumb actually hits; padding the button instead
      would push the title across.
    -->
    <button
      v-if="canDeliver"
      type="button"
      class="relative flex size-[26px] shrink-0 items-center justify-center rounded-full bg-(--q2-accent-solid) text-(--q2-accent-contrast) transition-colors after:absolute after:-inset-2.5 after:content-[''] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="`${t.proof.deliver}: ${goal.title}`"
      data-testid="window-deliver"
      @click="emit('deliver', goal.id)"
    >
      <UIcon
        name="i-lucide-camera"
        class="size-3.5"
        aria-hidden="true"
      />
    </button>

    <span
      v-else
      class="flex size-[26px] shrink-0 items-center justify-center rounded-full border-2"
      :class="isDelivered
        ? 'border-(--ui-text) bg-(--ui-text)'
        : 'border-(--ui-border-accented) bg-transparent'"
      :aria-label="isDelivered ? t.proof.confirmed : t.proof.waiting"
      role="img"
      data-testid="window-state"
    >
      <UIcon
        :name="isDelivered ? 'i-lucide-check' : 'i-lucide-hourglass'"
        class="size-3.5"
        :class="isDelivered ? 'text-(--ui-bg)' : 'text-(--ui-text-muted)'"
        aria-hidden="true"
      />
    </span>

    <NuxtLink
      :to="`/goals/${goal.id}`"
      class="min-w-0 flex-1 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    >
      <p
        class="text-sm font-bold"
        :class="isDelivered ? 'text-(--ui-text-dimmed) line-through' : 'text-(--ui-text)'"
        data-q2-private
      >
        {{ goal.title }}
      </p>

      <div class="mt-1 flex flex-wrap items-center gap-2">
        <!--
          The one label that is new, and it earns its place: "wird geprüft" is
          the state q2 could not previously express at all.
        -->
        <span
          v-if="isWaiting"
          class="inline-flex items-center gap-1 rounded-full bg-(--ui-bg-accented) px-2 py-0.5 text-[11px] font-bold text-(--ui-text-muted)"
          data-testid="window-waiting"
        >
          <UIcon
            name="i-lucide-hourglass"
            class="size-3"
            aria-hidden="true"
          />
          {{ t.proof.waiting }}
        </span>

        <span
          v-else
          class="inline-flex items-center gap-1 rounded-full bg-(--ui-bg-accented) px-2 py-0.5 text-[11px] font-bold text-(--ui-text-muted)"
        >
          <UIcon
            name="i-lucide-repeat"
            class="size-3"
            aria-hidden="true"
          />
          {{ scheduleLabel(goal.schedule, t) }}
        </span>

        <span
          v-if="currentWindow"
          class="text-[11px] font-bold text-(--ui-text-muted)"
          data-testid="window-remaining"
        >{{ windowLabel(currentWindow, t) }}</span>

        <span
          v-if="reminder && !isWaiting"
          class="flex items-center gap-1 text-[11px] font-semibold text-(--ui-text-dimmed)"
        >
          <UIcon
            name="i-lucide-clock"
            class="size-3"
            aria-hidden="true"
          />
          {{ reminder }}
        </span>
      </div>
    </NuxtLink>

    <!-- Only when the window wants more than one: "1 von 1" is a ring that says
         nothing the state circle has not already said. -->
    <AppProgressRing
      v-if="currentWindow && currentWindow.requiredProofs > 1"
      :percent="windowPercent(currentWindow)"
      :size="34"
      :label="windowLabel(currentWindow, t)"
    >
      <span class="text-[10px] font-extrabold">{{ currentWindow.confirmedProofs }}</span>
    </AppProgressRing>
  </div>
</template>
