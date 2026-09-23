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
  testId?: string
}>(), {
  icon: 'i-lucide-search',
  testId: undefined,
})

const model = defineModel<string>({ required: true })
</script>

<template>
  <UFormField
    :label="label"
    :name="id"
    :ui="{ label: 'sr-only' }"
  >
    <UInput
      :id="id"
      v-model="model"
      :placeholder="placeholder"
      :icon="icon"
      type="search"
      autocomplete="off"
      class="w-full"
      :ui="{ base: 'h-11 rounded-full bg-(--q2-track) ring-0 focus-visible:ring-2 focus-visible:ring-(--ui-primary)', leadingIcon: 'text-(--ui-text-dimmed)' }"
      :data-testid="testId"
    />
  </UFormField>
</template>
