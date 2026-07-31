<script setup lang="ts">
/**
 * The progress bar that appears on every goal, task and pinned chat.
 *
 * Small on purpose: the same number is drawn in five places, and all five have
 * to agree on rounding, clamping and what a screen reader hears.
 */
const props = withDefaults(defineProps<{
  percent: number
  /** Accessible description. Omit when adjacent text already says the number. */
  label?: string
  height?: number
}>(), {
  label: undefined,
  height: 8,
})

const value = computed(() => clampProgress(props.percent))
</script>

<template>
  <div
    class="w-full overflow-hidden rounded-full bg-(--q2-track)"
    :style="{ height: `${height}px` }"
    role="progressbar"
    :aria-valuenow="value"
    aria-valuemin="0"
    aria-valuemax="100"
    :aria-label="label"
    data-testid="progress-bar"
    data-q2-block
  >
    <div
      class="h-full rounded-full bg-(--ui-primary) transition-[width] duration-300"
      :style="{ width: `${value}%` }"
    />
  </div>
</template>
