<script setup lang="ts">
/**
 * The progress bar for a single goal.
 *
 * Small on purpose: progress is shown in the list, on the detail page and
 * (later) in reminders, and all three must agree on rounding, clamping and the
 * text a screen reader hears.
 */
const props = withDefaults(defineProps<{
  percent: number
  /** Hides the numeric label when the surrounding card already shows it. */
  showLabel?: boolean
  size?: 'sm' | 'md'
}>(), {
  showLabel: true,
  size: 'md',
})

const value = computed(() => clampProgress(props.percent))
const description = computed(() => describeProgress(props.percent))
</script>

<template>
  <div class="flex flex-col gap-1">
    <div
      v-if="showLabel"
      class="flex items-center justify-between text-sm text-(--ui-text-muted)"
    >
      <span>Progress</span>
      <span class="font-medium tabular-nums text-(--ui-text)">{{ value }}%</span>
    </div>

    <!--
      When the numeric label is hidden, the bar is the only thing left, so the
      description is provided for screen readers. With the label visible the
      adjacent "Progress / 62%" text already says it, and repeating it here
      would just be announced twice.

      UProgress itself supplies role="progressbar" and aria-valuenow/min/max.
      Note that it sets its own aria-label on that inner element, so passing one
      in would land on the wrapper, where nothing reads it.
    -->
    <span
      v-if="!showLabel"
      class="sr-only"
    >{{ description }}</span>

    <UProgress
      :model-value="value"
      :max="100"
      :size="size"
      color="primary"
    />
  </div>
</template>
