<script setup lang="ts">
/** One Nuxt UI switch treatment, including a full 44px touch target. */
defineProps<{ label: string }>()
const model = defineModel<boolean>({ required: true })
</script>

<template>
  <USwitch
    v-model="model"
    :aria-label="label"
    color="neutral"
    :ui="{
      base: 'h-[26px] w-11 after:absolute after:inset-x-0 after:-inset-y-[9px] after:content-[\'\']',
      thumb: 'size-5 data-[state=checked]:translate-x-[18px]',
    }"
    data-testid="toggle"
  />
</template>
