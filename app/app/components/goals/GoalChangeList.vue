<script setup lang="ts">
import type { GoalChangeItem } from '~/utils/goalChange'

/**
 * What an edit changed, one line each, from what to what — on the goal's
 * banner, in the edit sheet's summary and under the line in the conversation
 * (docs/adr/0044-editing-a-running-goal.md). Grey: a change is a state, not
 * something to do.
 */
withDefaults(defineProps<{
  items: readonly GoalChangeItem[]
  /** Centred, for the conversation's lines; left-aligned everywhere else. */
  centred?: boolean
}>(), {
  centred: false,
})
</script>

<template>
  <ul
    class="flex list-none flex-col gap-1 p-0 text-[13px] font-semibold text-(--ui-text-muted)"
    :class="centred ? 'items-center text-center' : ''"
    data-testid="goal-change-items"
    data-q2-private
  >
    <li
      v-for="item in items"
      :key="item.key"
      class="flex items-start gap-2"
      :data-testid="`goal-change-${item.key}`"
    >
      <UIcon
        :name="item.icon"
        class="mt-0.5 size-4 shrink-0"
        aria-hidden="true"
      />
      <span class="min-w-0 text-pretty">{{ item.text }}</span>
    </li>
  </ul>
</template>
