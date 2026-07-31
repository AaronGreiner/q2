<script setup lang="ts">
/**
 * The pill switch used for reminders and notification settings.
 *
 * Hand-built rather than `USwitch` for one reason: the design's switch is
 * larger than Nuxt UI's and sits at the end of a full-width row, and a
 * component whose only job is to be a different size is easier to read than a
 * `:ui` override threaded through four call sites.
 */
defineProps<{
  /** Names the switch for anyone who cannot see the row label next to it. */
  label: string
}>()

const model = defineModel<boolean>({ required: true })
</script>

<template>
  <button
    type="button"
    role="switch"
    :aria-checked="model"
    :aria-label="label"
    class="flex h-[26px] w-11 shrink-0 items-center rounded-full p-[3px] transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    :class="model ? 'justify-end bg-(--q2-accent-solid)' : 'justify-start bg-(--q2-track)'"
    data-testid="toggle"
    @click="model = !model"
  >
    <span class="size-5 rounded-full bg-white shadow-sm" />
  </button>
</template>
