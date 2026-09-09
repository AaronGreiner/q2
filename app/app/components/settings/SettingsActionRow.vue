<script setup lang="ts">
/**
 * One row in the settings list that does something when it is tapped.
 *
 * The same shape as `SettingsToggleRow` — icon tile, label, control on the
 * right — but a real button, so it reaches the keyboard and announces itself as
 * one. The chevron is the only difference a person sees, and it is what says
 * "this opens something" rather than "this changes a value here".
 */
defineProps<{
  icon: string
  label: string
  /** While the thing behind the row is being brought up. */
  busy?: boolean
}>()

defineEmits<{ activate: [] }>()
</script>

<template>
  <button
    type="button"
    class="flex w-full items-center gap-3 border-b border-(--ui-border) px-4 py-3 text-left last:border-b-0 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-(--ui-primary) disabled:opacity-55"
    :disabled="busy"
    @click="$emit('activate')"
  >
    <span
      class="flex size-9 shrink-0 items-center justify-center rounded-(--q2-radius-sm) bg-(--ui-bg-accented) text-(--ui-text)"
      aria-hidden="true"
    >
      <UIcon
        :name="icon"
        class="size-[18px]"
      />
    </span>

    <span class="min-w-0 flex-1 text-sm font-semibold">{{ label }}</span>

    <UIcon
      :name="busy ? 'i-lucide-loader-circle' : 'i-lucide-chevron-right'"
      class="size-4 shrink-0 text-(--ui-text-dimmed)"
      :class="busy && 'animate-spin'"
      aria-hidden="true"
    />
  </button>
</template>
