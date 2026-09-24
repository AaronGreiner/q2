<script setup lang="ts">
import type { GoalWindow } from '~/api/types'

/**
 * The windows a goal has been through, oldest on the left.
 *
 * A grid of squares rather than a list of rows: what somebody wants from a
 * history is the shape of it — a run, a gap, a run — and twenty rows of
 * "Geschafft" says that far less well than twenty squares does.
 *
 * **No accent.** A delivered window is a state, not something to do, so it is
 * drawn in white on a raised tile and a missed one is left dark. Colouring the
 * grid was where the design this comes from lost the accent's meaning: a
 * profile full of glowing squares makes the one button that matters look like
 * everything else.
 */
const props = defineProps<{
  /** Newest first, as the API returns them. */
  history: GoalWindow[]
  /** How many to show. The rest stay in the record but not on the screen. */
  limit?: number
}>()

const t = useMessages()

// Reversed, so the row reads left to right the way a calendar does.
const shown = computed(() => [...props.history].slice(0, props.limit ?? 28).reverse())

/**
 * The squares by month, each with its day on it. Without the dates a history
 * only said "a run, then a gap" — not when, which is the first thing anybody
 * asks of a gap.
 */
const months = computed(() => {
  const groups: { key: string, label: string, entries: GoalWindow[] }[] = []

  for (const entry of shown.value) {
    const key = entry.dueOn.slice(0, 7)
    const last = groups.at(-1)

    if (last?.key === key) {
      last.entries.push(entry)
      continue
    }

    const month = Number(key.slice(5, 7))
    groups.push({ key, label: t.value.proofGallery.months[month - 1] ?? key, entries: [entry] })
  }

  return groups
})

function dayOf(day: string): number {
  return Number(day.slice(8, 10))
}
</script>

<template>
  <div
    class="flex flex-col gap-3"
    data-testid="history-grid"
    data-q2-block
  >
    <section
      v-for="month in months"
      :key="month.key"
      :aria-label="month.label"
    >
      <p
        class="mb-1.5 px-0.5 text-[11px] font-bold text-(--ui-text-muted)"
        aria-hidden="true"
      >
        {{ month.label }}
      </p>

      <ul class="flex list-none flex-wrap gap-1.5 p-0">
        <li
          v-for="entry in month.entries"
          :key="entry.id"
          class="flex h-10 w-8 flex-col items-center justify-center gap-0.5 rounded-(--q2-radius-sm)"
          :class="entry.status === 'Done'
            ? 'bg-(--ui-text-toned) text-(--ui-bg)'
            : 'bg-(--ui-bg-accented) text-(--ui-text-dimmed)'"
          :title="`${formatDay(entry.dueOn)} — ${windowOutcome(entry, t).label}`"
          data-testid="history-cell"
        >
          <!--
            The icon is not the only carrier: the title says the outcome in
            words, and so does the text a screen reader gets. Status is never
            colour alone here, and it is never shape alone either.
          -->
          <UIcon
            :name="windowOutcome(entry, t).icon"
            class="size-3.5"
            aria-hidden="true"
          />
          <span
            class="text-[9px] leading-none font-bold"
            aria-hidden="true"
          >{{ dayOf(entry.dueOn) }}</span>
          <span class="sr-only">{{ formatDay(entry.dueOn) }}: {{ windowOutcome(entry, t).label }}</span>
        </li>
      </ul>
    </section>
  </div>
</template>
