<script setup lang="ts">
/**
 * One switch in the settings list: icon, label, toggle.
 */
defineProps<{
  icon: string
  label: string
}>()

const model = defineModel<boolean>({ required: true })
</script>

<template>
  <div class="flex items-center gap-3 border-b border-(--ui-border) px-4 py-3 last:border-b-0">
    <span
      class="flex size-9 shrink-0 items-center justify-center rounded-(--q2-radius-sm) bg-(--q2-accent-soft) text-(--q2-accent-soft-text)"
      aria-hidden="true"
    >
      <UIcon
        :name="icon"
        class="size-[18px]"
      />
    </span>

    <span class="min-w-0 flex-1 text-sm font-semibold">{{ label }}</span>

    <AppToggle
      v-model="model"
      :label="label"
    />
  </div>
</template>
