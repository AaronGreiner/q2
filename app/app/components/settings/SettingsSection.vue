<script setup lang="ts">
/**
 * A titled group of settings.
 *
 * The uppercase caption above a card is the whole pattern the settings screen
 * repeats, and putting it here is what keeps the caption a real heading rather
 * than a styled paragraph — so the screen can be navigated by heading.
 */
defineProps<{
  title: string
  /** Explains a group whose switches do not do anything yet. */
  note?: string
}>()
</script>

<template>
  <section class="mt-5 first:mt-0">
    <h2 class="mx-1 mb-2 text-xs font-extrabold tracking-wide text-(--ui-text-muted) uppercase">
      {{ title }}
    </h2>

    <div class="q2-card overflow-hidden">
      <slot />
    </div>

    <p
      v-if="note"
      class="mx-1 mt-2 text-xs text-(--ui-text-muted)"
    >
      {{ note }}
    </p>
  </section>
</template>
