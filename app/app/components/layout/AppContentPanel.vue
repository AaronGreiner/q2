<script setup lang="ts">
/** The continuous Ruhe surface, with an optional fixed toolbar above its scroll area. */
</script>

<template>
  <UCard
    class="flex min-h-0 flex-1 flex-col overflow-hidden rounded-t-(--q2-radius-panel) rounded-b-none bg-(--q2-surface) shadow-none ring-0"
    :ui="{
      header: 'shrink-0 border-0 px-5 pt-4 pb-2 sm:px-5',
      body: ['q2-content-arrival q2-scroll min-h-0 flex-1 px-5 pt-4 pb-8 sm:p-5', $slots.toolbar ? 'pt-0 sm:pt-0' : ''],
    }"
    data-testid="content-panel"
  >
    <template
      v-if="$slots.toolbar"
      #header
    >
      <slot name="toolbar" />
    </template>
    <slot />
  </UCard>
</template>
