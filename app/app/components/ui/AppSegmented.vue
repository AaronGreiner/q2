<script setup lang="ts" generic="T extends string">
/** Nuxt UI owns radio semantics and arrow-key navigation; q2 supplies the quiet treatment. */
const props = defineProps<{
  options: readonly { value: T, label: string, icon?: string }[]
  label: string
}>()
const model = defineModel<T>({ required: true })
const selectedIndex = computed(() => props.options.findIndex(option => option.value === model.value))
const items = computed<{ value: string, label: string, icon?: string }[]>(() => [...props.options])
const selected = computed<string>({
  get: () => model.value,
  set: (value) => {
    const option = props.options.find(option => option.value === value)
    if (option) model.value = option.value
  },
})
</script>

<template>
  <URadioGroup
    v-model="selected"
    class="q2-segmented"
    :style="{ '--q2-segment-count': options.length, '--q2-segment-index': selectedIndex }"
    :items="items"
    :aria-label="label"
    orientation="horizontal"
    variant="card"
    indicator="hidden"
    color="neutral"
    :ui="{
      fieldset: 'relative isolate gap-1 rounded-full bg-(--q2-track) p-1',
      item: 'relative min-h-11 min-w-0 flex-1 items-center justify-center rounded-full border-0 px-2 py-2.5 has-focus-visible:ring-2 has-focus-visible:ring-(--ui-primary)',
      label: 'flex items-center justify-center gap-1.5 text-[13px] font-semibold',
      container: 'h-0',
    }"
  >
    <template #label="{ item }">
      <span
        class="flex items-center justify-center gap-1.5"
        :data-testid="`segment-${item.value}`"
      >
        <UIcon
          v-if="item.icon"
          :name="item.icon"
          class="size-4"
          aria-hidden="true"
        />
        {{ item.label }}
      </span>
    </template>
  </URadioGroup>
</template>
