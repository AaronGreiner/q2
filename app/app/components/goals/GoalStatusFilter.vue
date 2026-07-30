<script setup lang="ts">
import { goalStatuses, type GoalStatus } from '~/api/types'

/**
 * Filters the goal list by status. `undefined` means "all".
 *
 * Rendered as a radio group rather than a select: there are four options, they
 * benefit from being visible at once, and arrow-key navigation between radios
 * is what a keyboard user expects from a filter.
 */
const model = defineModel<GoalStatus | undefined>({ default: undefined })

const options = computed(() => [
  { value: undefined, label: 'All' },
  ...goalStatuses.map(status => ({
    value: status as GoalStatus | undefined,
    label: goalStatusPresentation(status).label,
  })),
])
</script>

<template>
  <div
    class="flex flex-wrap items-center gap-1"
    role="radiogroup"
    aria-label="Filter goals by status"
    data-testid="goal-status-filter"
  >
    <UButton
      v-for="option in options"
      :key="option.label"
      :variant="model === option.value ? 'solid' : 'ghost'"
      :color="model === option.value ? 'primary' : 'neutral'"
      size="sm"
      role="radio"
      :aria-checked="model === option.value"
      :data-testid="`filter-${option.label.toLowerCase()}`"
      @click="model = option.value"
    >
      {{ option.label }}
    </UButton>
  </div>
</template>
