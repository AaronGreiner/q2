<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'

/**
 * Moving a goal's daily reminder, or taking it away.
 *
 * The one thing about a running goal its owner changes on their own: it nudges
 * only them, so nobody who agreed to check the goal agreed to anything it
 * touches. The time is the phone's own picker (`type="time"`), which is what a
 * thumb expects and needs no second implementation of hours and minutes.
 */
const props = withDefaults(defineProps<{
  /** The reminder as the server has it, `HH:MM:SS`, or null for none. */
  current: string | null
  submitting?: boolean
  failure?: ApiFailure | null
}>(), {
  submitting: false,
  failure: null,
})

const emit = defineEmits<{ save: [reminderAt: string | null] }>()

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()

const time = ref('')

// Kept mounted between openings, so it starts from what is stored each time
// rather than from last time's unsaved draft.
watch(open, (isOpen) => {
  if (isOpen) time.value = formatClock(props.current) ?? '09:00'
}, { immediate: true })

const canSave = computed(() => /^\d{2}:\d{2}$/.test(time.value) && !props.submitting)

function onSave() {
  if (!canSave.value) return
  emit('save', `${time.value}:00`)
}
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="t.goals.reminderEdit"
    :description="t.goals.reminderSubtitle"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <form
        class="flex flex-col gap-4 pb-2"
        novalidate
        data-testid="goal-reminder-form"
        @submit.prevent="onSave"
      >
        <UFormField
          :label="t.goals.reminderTime"
          name="reminderAt"
        >
          <UInput
            v-model="time"
            type="time"
            size="xl"
            class="w-full"
            data-testid="goal-reminder-time"
          />
        </UFormField>

        <p
          v-if="failure"
          class="text-sm text-(--ui-error)"
          role="alert"
        >
          {{ t.errors[failure.kind] }}
        </p>

        <UButton
          type="submit"
          size="xl"
          class="justify-center"
          :loading="submitting"
          :disabled="!canSave"
          data-testid="goal-reminder-save"
        >
          {{ t.goals.reminderSave }}
        </UButton>

        <!-- Grey: taking the reminder away is a choice, not a loss. -->
        <UButton
          v-if="current"
          type="button"
          size="lg"
          color="neutral"
          variant="ghost"
          icon="i-lucide-bell-off"
          class="justify-center"
          :disabled="submitting"
          data-testid="goal-reminder-remove"
          @click="emit('save', null)"
        >
          {{ t.goals.reminderRemove }}
        </UButton>
      </form>
    </template>
  </UDrawer>
</template>
