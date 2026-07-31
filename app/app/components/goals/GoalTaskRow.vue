<script setup lang="ts">
import type { GoalTask } from '~/api/types'

/**
 * One task with its tick box.
 *
 * The same row on the start screen, on the goals screen and on a goal — which
 * is the point: ticking something off has to feel identical wherever it
 * happens, including what a screen reader says about it.
 */
const props = withDefaults(defineProps<{
  task: GoalTask
  /** Shows the measure bar. Off in the compact list on the start screen. */
  detailed?: boolean
  busy?: boolean
}>(), {
  detailed: false,
  busy: false,
})

const emit = defineEmits<{ toggle: [id: string] }>()

const t = useMessages()

const time = computed(() => formatClock(props.task.reminderAt))
const measure = computed(() => formatMeasure(props.task, t.value.numbers.decimal))
</script>

<template>
  <div
    class="q2-card flex items-center gap-3 px-3.5 py-3"
    data-testid="task-row"
  >
    <!--
      A real checkbox, not a div with a click handler: the tick is the whole
      point of this row, and it has to be reachable by keyboard, announced as
      checked or unchecked, and toggled with the space bar.
    -->
    <button
      type="button"
      role="checkbox"
      :aria-checked="task.isDone"
      :disabled="busy"
      class="flex size-[26px] shrink-0 items-center justify-center rounded-full border-2 transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary) disabled:opacity-60"
      :class="task.isDone
        ? 'border-(--q2-accent-solid) bg-(--q2-accent-solid)'
        : 'border-(--ui-border-accented) bg-transparent'"
      :aria-label="task.title"
      data-testid="task-toggle"
      @click="emit('toggle', task.id)"
    >
      <UIcon
        v-if="task.isDone"
        name="i-lucide-check"
        class="size-4 text-white"
        aria-hidden="true"
      />
    </button>

    <div class="min-w-0 flex-1">
      <p
        class="text-sm font-bold"
        :class="task.isDone ? 'text-(--ui-text-dimmed) line-through' : 'text-(--ui-text)'"
      >
        {{ task.title }}
      </p>

      <div class="mt-1 flex flex-wrap items-center gap-2">
        <span class="inline-flex items-center gap-1 rounded-full bg-(--q2-accent-soft) px-2 py-0.5 text-[11px] font-bold text-(--q2-accent-soft-text)">
          <UIcon
            name="i-lucide-repeat"
            class="size-3"
            aria-hidden="true"
          />
          {{ t.rhythm[task.rhythm] }}
        </span>

        <span
          v-if="time"
          class="inline-flex items-center gap-1 text-[11px] font-semibold text-(--ui-text-muted)"
        >
          <UIcon
            name="i-lucide-clock"
            class="size-3"
            aria-hidden="true"
          />
          {{ time }}
        </span>
      </div>

      <div
        v-if="detailed && measure"
        class="mt-2 flex items-center gap-2"
      >
        <AppProgressBar
          :percent="task.measurePercent ?? 0"
          :height="6"
          :label="measure"
        />
        <span class="shrink-0 text-[11px] font-bold text-(--ui-text-muted)">{{ measure }}</span>
      </div>
    </div>

    <span
      v-if="!detailed && measure"
      class="shrink-0 text-xs font-bold text-(--ui-text-muted)"
    >{{ measure }}</span>
  </div>
</template>
