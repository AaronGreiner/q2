<script setup lang="ts">
/**
 * The shared shell for "there is nothing to show" states — empty, error and
 * not-found. One component so the three never drift apart in spacing, heading
 * level or focus behaviour.
 *
 * `role="status"` (polite) is used for informational states and
 * `role="alert"` (assertive) for errors, so a screen reader interrupts only
 * when something actually went wrong.
 */
withDefaults(defineProps<{
  icon: string
  title: string
  description?: string
  tone?: 'neutral' | 'error'
}>(), {
  description: undefined,
  tone: 'neutral',
})
</script>

<template>
  <div
    class="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-(--ui-border) px-5 py-10 text-center"
    :role="tone === 'error' ? 'alert' : 'status'"
  >
    <UIcon
      :name="icon"
      class="size-8"
      :class="tone === 'error' ? 'text-(--ui-error)' : 'text-(--ui-text-dimmed)'"
      aria-hidden="true"
    />

    <div class="flex flex-col gap-1">
      <p class="font-semibold text-(--ui-text)">
        {{ title }}
      </p>
      <p
        v-if="description"
        class="text-sm text-balance text-(--ui-text-muted)"
      >
        {{ description }}
      </p>
    </div>

    <!-- Recovery actions: a retry button, a link back, and so on. -->
    <slot />
  </div>
</template>
