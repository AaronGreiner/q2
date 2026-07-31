<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'
import type { CreateGoalRequest, GoalRhythm } from '~/api/types'
import { goalIcons, goalRhythms } from '~/api/types'

/**
 * The sheet that slides up from the bottom to create a goal.
 *
 * It owns its own draft and nothing else: submitting emits the request and the
 * page decides what to do with it. Server-side field errors are passed back in
 * via `error`, so the messages the user reads are the ones the API produced —
 * no second copy of the rules living in the browser.
 */
const props = withDefaults(defineProps<{
  submitting?: boolean
  error?: ApiFailure | null
}>(), {
  submitting: false,
  error: null,
})

const emit = defineEmits<{ submit: [request: CreateGoalRequest] }>()

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()

const maxTitleLength = 120

const title = ref('')
const rhythm = ref<GoalRhythm>('Daily')
const icon = ref<string>(goalIcons[0]!)
const totalSteps = ref(30)
const withReminder = ref(true)

/** Server-reported messages for one field, matched case-insensitively. */
function fieldError(field: string): string | undefined {
  const errors = props.error?.fieldErrors ?? {}
  const match = Object.keys(errors).find(key => key.toLowerCase() === field.toLowerCase())
  return match ? errors[match]?.[0] : undefined
}

/** Only blocks the obviously empty case; the API remains the authority. */
const canSubmit = computed(() => title.value.trim().length > 0 && !props.submitting)

function onSubmit() {
  if (!canSubmit.value) return

  emit('submit', {
    title: title.value.trim(),
    rhythm: rhythm.value,
    icon: icon.value,
    totalSteps: totalSteps.value,

    // A daily nudge at nine, or none at all. Choosing the hour is a setting
    // this version does not have, and inventing a time picker for it would be
    // building ahead of the requirement.
    reminderAt: withReminder.value ? '09:00:00' : null,
  })
}

/** Called by the page after a successful create. */
function reset() {
  title.value = ''
  rhythm.value = 'Daily'
  icon.value = goalIcons[0]!
  totalSteps.value = 30
  withReminder.value = true
}

defineExpose({ reset })
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="t.create.heading"
    :description="t.create.subtitle"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <form
        class="flex flex-col gap-4 pb-2"
        novalidate
        data-testid="goal-create-form"
        @submit.prevent="onSubmit"
      >
        <UFormField
          :label="t.create.titleLabel"
          name="title"
          required
          :error="fieldError('title')"
          :hint="`${title.length}/${maxTitleLength}`"
        >
          <UInput
            v-model="title"
            :placeholder="t.create.titlePlaceholder"
            :maxlength="maxTitleLength"
            autocomplete="off"
            size="xl"
            class="w-full"
            data-testid="goal-title-input"
          />
        </UFormField>

        <UFormField
          :label="t.create.rhythmLabel"
          name="rhythm"
        >
          <div
            class="flex flex-wrap gap-2"
            role="radiogroup"
            :aria-label="t.create.rhythmLabel"
          >
            <button
              v-for="option in goalRhythms"
              :key="option"
              type="button"
              role="radio"
              :aria-checked="rhythm === option"
              class="rounded-full border px-3.5 py-2 text-[13px] font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
              :class="rhythm === option
                ? 'border-transparent bg-(--q2-accent-solid) text-white'
                : 'border-(--ui-border) bg-(--q2-surface) text-(--ui-text-muted)'"
              :data-testid="`rhythm-${option}`"
              @click="rhythm = option"
            >
              {{ t.rhythm[option] }}
            </button>
          </div>
        </UFormField>

        <UFormField
          :label="t.create.iconLabel"
          name="icon"
          :error="fieldError('icon')"
        >
          <div
            class="grid grid-cols-6 gap-2"
            role="radiogroup"
            :aria-label="t.create.iconLabel"
          >
            <button
              v-for="option in goalIcons"
              :key="option"
              type="button"
              role="radio"
              :aria-checked="icon === option"
              :aria-label="option"
              class="flex aspect-square items-center justify-center rounded-(--q2-radius-md) border focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
              :class="icon === option
                ? 'border-transparent bg-(--q2-accent-solid) text-white'
                : 'border-(--ui-border) bg-(--q2-surface) text-(--ui-text-muted)'"
              :data-testid="`icon-${option}`"
              @click="icon = option"
            >
              <UIcon
                :name="goalIconName(option)"
                class="size-5"
              />
            </button>
          </div>
        </UFormField>

        <UFormField
          :label="t.create.stepsLabel"
          name="totalSteps"
          :help="t.create.stepsHelp"
          :error="fieldError('totalSteps')"
        >
          <UInputNumber
            v-model="totalSteps"
            :min="1"
            :max="1000"
            size="xl"
            class="w-full"
            data-testid="goal-steps-input"
          />
        </UFormField>

        <div class="flex items-center justify-between rounded-(--q2-radius-lg) border border-(--ui-border) bg-(--q2-surface) px-4 py-3">
          <span class="flex items-center gap-2.5">
            <UIcon
              name="i-lucide-bell"
              class="size-5 text-(--ui-primary)"
              aria-hidden="true"
            />
            <span class="text-sm font-semibold">{{ t.create.reminderLabel }}</span>
          </span>

          <AppToggle
            v-model="withReminder"
            :label="t.create.reminderLabel"
          />
        </div>

        <!--
          A non-field error (offline, 5xx) belongs above the button where the
          user is looking after pressing it.
        -->
        <p
          v-if="error && Object.keys(error.fieldErrors).length === 0"
          class="text-sm text-(--ui-error)"
          role="alert"
          data-testid="goal-create-error"
        >
          {{ t.errors[error.kind] }}
        </p>

        <UButton
          type="submit"
          icon="i-lucide-sparkles"
          size="xl"
          class="justify-center"
          :loading="submitting"
          :disabled="!canSubmit"
          data-testid="goal-submit"
        >
          {{ t.create.submit }}
        </UButton>
      </form>
    </template>
  </UDrawer>
</template>
