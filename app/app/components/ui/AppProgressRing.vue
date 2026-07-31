<script setup lang="ts">
/**
 * The percentage ring on the home screen and on a goal.
 *
 * A conic gradient rather than an SVG arc: it is one CSS property, it animates,
 * and it needs no viewBox arithmetic to stay round at two different sizes.
 */
const props = withDefaults(defineProps<{
  percent: number
  size?: number
  /** Accessible description of what the ring is measuring. */
  label: string
}>(), {
  size: 92,
})

const value = computed(() => clampProgress(props.percent))
const inner = computed(() => Math.round(props.size * 0.76))
</script>

<template>
  <div
    class="relative flex shrink-0 items-center justify-center rounded-full"
    :style="{
      width: `${size}px`,
      height: `${size}px`,
      background: `conic-gradient(var(--ui-primary) ${value * 3.6}deg, var(--q2-track) 0deg)`,
    }"
    role="progressbar"
    :aria-valuenow="value"
    aria-valuemin="0"
    aria-valuemax="100"
    :aria-label="label"
    data-testid="progress-ring"
    data-q2-block
  >
    <div
      class="flex flex-col items-center justify-center rounded-full bg-(--q2-surface)"
      :style="{ width: `${inner}px`, height: `${inner}px` }"
    >
      <slot :value="value" />
    </div>
  </div>
</template>
