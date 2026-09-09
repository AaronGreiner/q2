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
</script>

<template>
  <ul
    class="flex list-none flex-wrap gap-1.5 p-0"
    data-testid="history-grid"
    data-q2-block
  >
    <li
      v-for="entry in shown"
      :key="entry.id"
      class="flex size-7 items-center justify-center rounded-(--q2-radius-sm)"
      :class="entry.status === 'Done'
        ? 'bg-(--ui-text-toned) text-(--ui-bg)'
        : 'bg-(--ui-bg-accented) text-(--ui-text-dimmed)'"
      :title="`${formatDay(entry.dueOn)} — ${windowOutcome(entry, t).label}`"
    >
      <!--
        The icon is not the only carrier: the title says the outcome in words,
        and the summary under the grid counts both. Status is never colour
        alone here, and it is never shape alone either.
      -->
      <UIcon
        :name="windowOutcome(entry, t).icon"
        class="size-3.5"
        aria-hidden="true"
      />
      <span class="sr-only">{{ formatDay(entry.dueOn) }}: {{ windowOutcome(entry, t).label }}</span>
    </li>
  </ul>
</template>
