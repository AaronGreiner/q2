<script setup lang="ts">
import { goalIcons } from '~/api/types'

/**
 * Which of the goal icons a goal is drawn with — a closed set, because the
 * server validates against its own list (`GoalIcons`) and the bundle only
 * holds those. Used where a goal is made and where it is edited.
 */
defineProps<{
  /** The radio group's accessible name; the field around it shows the same words. */
  label: string
}>()

const model = defineModel<string>({ required: true })
</script>

<template>
  <div
    class="grid grid-cols-6 gap-2"
    role="radiogroup"
    :aria-label="label"
  >
    <button
      v-for="option in goalIcons"
      :key="option"
      type="button"
      role="radio"
      :aria-checked="model === option"
      :aria-label="option"
      class="flex aspect-square items-center justify-center rounded-(--q2-radius-md) border focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :class="model === option
        ? 'border-transparent bg-(--q2-accent-solid) text-(--q2-accent-contrast)'
        : 'border-(--ui-border) bg-(--q2-surface) text-(--ui-text-muted)'"
      :data-testid="`icon-${option}`"
      @click="model = option"
    >
      <UIcon
        :name="goalIconName(option)"
        class="size-5"
      />
    </button>
  </div>
</template>
