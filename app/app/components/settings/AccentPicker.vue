<script setup lang="ts">
import { accentNames, type AccentName } from '~/composables/useAccent'

const model = defineModel<AccentName>({ required: true })
const t = useMessages()
const noteId = useId()
const items = computed(() => accentNames.map(value => ({
  value,
  label: t.value.settings.accents[value],
})))
</script>

<template>
  <URadioGroup
    v-model="model"
    :items="items"
    :legend="t.settings.accent"
    :aria-describedby="noteId"
    orientation="horizontal"
    variant="card"
    indicator="hidden"
    :ui="{
      fieldset: 'grid grid-cols-4 gap-2',
      item: 'min-h-20 justify-center rounded-(--q2-radius-md) p-2 ring-(--ui-border) has-data-[state=checked]:bg-(--q2-accent-soft) has-focus-visible:ring-2 has-focus-visible:ring-(--ui-primary)',
      label: 'flex flex-col items-center gap-2 text-xs font-semibold',
      legend: 'mb-3 text-sm font-bold',
    }"
    data-testid="accent-picker"
  >
    <template #label="{ item }">
      <span
        class="flex size-7 items-center justify-center rounded-full text-(--q2-accent-contrast)"
        :style="{ background: `var(--q2-swatch-${item.value})` }"
        aria-hidden="true"
      >
        <UIcon
          v-if="model === item.value"
          name="i-lucide-check"
          class="size-4"
        />
      </span>
      {{ item.label }}
    </template>
  </URadioGroup>
  <p
    :id="noteId"
    class="mt-2 text-xs leading-relaxed text-(--ui-text-muted)"
  >
    {{ t.settings.accentNote }}
  </p>
</template>
