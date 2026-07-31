<script setup lang="ts">
/**
 * The search box that sits under a screen's title.
 *
 * It exists as a component because there are two of them — the chat list and
 * the friends screen — and they had drifted: one was 40px tall, the other 44,
 * with different icon sizes and a different inset from the edge of the screen.
 * Two boxes doing the same job on two tabs of the same app have to be the same
 * box.
 *
 * The icon is a prop rather than fixed, because it is the one thing the two
 * genuinely disagree about: the friends box searches everybody in q2 rather
 * than filtering what is on screen, and a magnifier would suggest otherwise.
 */
withDefaults(defineProps<{
  /** Ties the visually hidden label to the field. */
  id: string
  /** Read out in place of a placeholder, which no screen reader promises. */
  label: string
  placeholder: string
  icon?: string
  /** Coloured with the primary when the icon is doing more than decorating. */
  accent?: boolean
  testId?: string
}>(), {
  icon: 'i-lucide-search',
  accent: false,
  testId: undefined,
})

const model = defineModel<string>({ required: true })
</script>

<template>
  <div>
    <label
      class="sr-only"
      :for="id"
    >{{ label }}</label>

    <div class="flex h-11 items-center gap-2.5 rounded-full border border-(--ui-border) bg-(--q2-surface) px-3.5">
      <UIcon
        :name="icon"
        class="size-[18px] shrink-0"
        :class="accent ? 'text-(--ui-primary)' : 'text-(--ui-text-dimmed)'"
        aria-hidden="true"
      />

      <input
        :id="id"
        v-model="model"
        :placeholder="placeholder"
        type="search"
        autocomplete="off"
        class="min-w-0 flex-1 bg-transparent text-sm text-(--ui-text) outline-none"
        :data-testid="testId"
      >
    </div>
  </div>
</template>
