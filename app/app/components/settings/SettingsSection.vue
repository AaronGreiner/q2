<script setup lang="ts">
/**
 * A titled group of settings.
 *
 * The quiet caption above a group is the whole pattern the settings screen
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
  <section class="mt-6 first:mt-0">
    <h2 class="mx-1 mb-2 text-xs font-semibold text-(--ui-text-muted)">
      {{ title }}
    </h2>

    <UCard
      class="overflow-hidden rounded-none bg-transparent shadow-none ring-0"
      :ui="{ body: 'p-0 sm:p-0' }"
    >
      <slot />
    </UCard>

    <p
      v-if="note"
      class="mx-1 mt-2 text-xs text-(--ui-text-muted)"
    >
      {{ note }}
    </p>
  </section>
</template>
