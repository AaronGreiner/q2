<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'

/**
 * Asking for a pause: a reason, and how many days.
 *
 * The two costs the rule leans on are both on this screen rather than behind
 * it. The reason is a field that will not submit until it is a sentence, and
 * the allowance is a number in plain sight — "noch 1 Pause diesen Monat" is
 * what makes somebody spend it on the week they are actually ill.
 *
 * There is deliberately no wording that treats this as a confession. Somebody
 * setting a goal aside has not done anything wrong; the friction is the
 * scarcity, not the tone.
 */
const props = defineProps<{
  /** How many pauses are left this month, as the server counted them. */
  remaining: number
  maxDays: number
  minReason: number
  submitting: boolean
  failure?: ApiFailure | null
}>()

const emit = defineEmits<{ submit: [value: { reason: string, days: number }] }>()

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()

const reason = ref('')
const days = ref(2)

// Kept mounted between openings, so the draft goes back to empty each time —
// otherwise last week's excuse is still in the field.
watch(open, (isOpen) => {
  if (isOpen) {
    reason.value = ''
    days.value = 2
  }
})

const trimmed = computed(() => reason.value.trim())
const isReasonTooShort = computed(() => trimmed.value.length < props.minReason)
const hasAllowance = computed(() => props.remaining > 0)
const canSubmit = computed(() => !isReasonTooShort.value && hasAllowance.value && !props.submitting)

const dayOptions = computed(() =>
  Array.from({ length: props.maxDays }, (_, index) => index + 1))

function onSubmit() {
  if (!canSubmit.value) return
  emit('submit', { reason: trimmed.value, days: days.value })
}
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="t.pause.heading"
    :description="t.pause.subtitle"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <div class="flex flex-col gap-5 pb-2">
        <p
          class="text-[13px] font-bold"
          :class="hasAllowance ? 'text-(--ui-text-muted)' : 'text-(--ui-error)'"
          data-testid="pause-allowance"
        >
          {{ hasAllowance ? t.pause.remaining(remaining) : t.pause.exhausted }}
        </p>

        <UFormField
          :label="t.pause.reasonLabel"
          :help="t.pause.reasonHint(minReason)"
        >
          <UTextarea
            v-model="reason"
            :rows="3"
            :maxlength="280"
            :placeholder="t.pause.reasonPlaceholder"
            :disabled="!hasAllowance"
            class="w-full"
            data-testid="pause-reason"
          />
        </UFormField>

        <!-- The same chips the create sheet uses for a quota, and drawn the
             same way: a native radio group rather than a row of buttons, so a
             screen reader hears one choice with seven options. -->
        <div class="flex flex-col gap-1.5">
          <p class="q2-eyebrow">
            {{ t.pause.daysLabel }}
          </p>

          <div
            class="flex flex-wrap gap-1.5"
            role="radiogroup"
            :aria-label="t.pause.daysLabel"
          >
            <button
              v-for="option in dayOptions"
              :key="option"
              type="button"
              role="radio"
              :aria-checked="option === days"
              :disabled="!hasAllowance"
              class="min-h-11 rounded-(--q2-radius-md) border px-3 text-[13px] font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary) disabled:opacity-50"
              :class="option === days
                ? 'border-transparent bg-(--q2-accent-solid) text-(--q2-accent-contrast)'
                : 'border-(--ui-border) bg-(--q2-surface) text-(--ui-text-muted)'"
              :data-testid="`pause-days-${option}`"
              @click="days = option"
            >
              {{ t.pause.days(option) }}
            </button>
          </div>
        </div>

        <p class="text-[13px] font-semibold text-(--ui-text-muted)">
          {{ t.pause.windowNote }}
        </p>

        <AppErrorState
          v-if="failure"
          :error="failure"
        />

        <UButton
          block
          size="xl"
          icon="i-lucide-pause"
          :label="t.pause.submit"
          :disabled="!canSubmit"
          :loading="submitting"
          data-testid="pause-submit"
          @click="onSubmit"
        />
      </div>
    </template>
  </UDrawer>
</template>
