<script setup lang="ts">
import type { GoalCategory } from '~/api/types'
import { goalCategories } from '~/api/types'

/**
 * Which of the five categories a goal is in (#18): what kind of thing it is
 * about, which also decides the icon it is drawn with. A closed set, because
 * the server knows exactly these. Used where a goal is made, where it is
 * edited, and for a first Do that waits.
 *
 * Words beside the icons, not icons alone: the choice now means something —
 * a badge can be earned with a kind of goal — and an icon is not a name.
 */
defineProps<{
  /** The radio group's accessible name; the field around it shows the same words. */
  label: string
}>()

const model = defineModel<GoalCategory>({ required: true })

const t = useMessages()
</script>

<template>
  <div
    class="flex flex-wrap gap-2"
    role="radiogroup"
    :aria-label="label"
  >
    <button
      v-for="option in goalCategories"
      :key="option"
      type="button"
      role="radio"
      :aria-checked="model === option"
      class="flex min-h-11 items-center gap-2 rounded-full border px-4 text-sm font-medium focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :class="model === option
        ? 'border-transparent bg-(--q2-accent-solid) text-(--q2-accent-contrast)'
        : 'border-(--ui-border) bg-(--q2-surface) text-(--ui-text-muted)'"
      :data-testid="`category-${option}`"
      @click="model = option"
    >
      <UIcon
        :name="categoryIconName(option)"
        class="size-4.5 shrink-0"
        aria-hidden="true"
      />
      {{ t.goalCategory[option] }}
    </button>
  </div>
</template>
