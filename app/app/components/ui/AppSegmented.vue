<script setup lang="ts" generic="T extends string">
/**
 * A segmented control: two or three options, all visible, one selected.
 *
 * Used for the goals tabs, the theme and the language. A radio group rather
 * than a set of buttons, because that is what it is — arrow keys move between
 * the options, and a screen reader announces "2 of 3" instead of reading three
 * unrelated buttons.
 */
defineProps<{
  options: readonly { value: T, label: string, icon?: string }[]
  /** Names the group for anyone who cannot see the heading above it. */
  label: string
}>()

const model = defineModel<T>({ required: true })
</script>

<template>
  <!--
    Track and marker are both pills, and that is the point: the marker sits
    inside the track with 4px around it, so any other pair of radii leaves a
    square-ish tab rattling around in a round groove. A pill is the one shape
    that stays concentric whatever the control's height turns out to be.
  -->
  <div
    class="flex gap-1 rounded-full bg-(--q2-track) p-1"
    role="radiogroup"
    :aria-label="label"
  >
    <button
      v-for="option in options"
      :key="option.value"
      type="button"
      role="radio"
      :aria-checked="model === option.value"
      class="flex min-h-11 flex-1 items-center justify-center gap-1.5 rounded-full px-2 py-2.5 text-[13px] font-bold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :class="model === option.value
        ? 'bg-(--q2-surface) text-(--ui-text) shadow-[var(--q2-card-shadow)]'
        : 'text-(--ui-text-muted)'"
      :data-testid="`segment-${option.value}`"
      @click="model = option.value"
    >
      <UIcon
        v-if="option.icon"
        :name="option.icon"
        class="size-4"
        aria-hidden="true"
      />
      {{ option.label }}
    </button>
  </div>
</template>
