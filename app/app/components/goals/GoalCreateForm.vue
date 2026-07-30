<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'
import type { CreateGoalRequest } from '~/api/types'

/**
 * The form for creating a goal.
 *
 * It owns its own draft state and nothing else: submitting emits the request
 * and the page decides what to do with it. Server-side field errors are passed
 * back in via `error`, so the same messages the API produced are the ones the
 * user reads — no second copy of the rules living in the browser.
 */
const props = withDefaults(defineProps<{
  submitting?: boolean
  error?: ApiFailure | null
}>(), {
  submitting: false,
  error: null,
})

const emit = defineEmits<{ submit: [request: CreateGoalRequest] }>()

const maxTitleLength = 120
const maxDescriptionLength = 1000

const title = ref('')
const description = ref('')
const targetDate = ref('')
const participants = ref('')

/** Server-reported messages for one field, matched case-insensitively. */
function fieldError(field: string): string | undefined {
  const errors = props.error?.fieldErrors ?? {}
  const match = Object.keys(errors).find(key => key.toLowerCase() === field.toLowerCase())
  return match ? errors[match]?.[0] : undefined
}

const titleError = computed(() => fieldError('title'))
const descriptionError = computed(() => fieldError('description'))
const participantsError = computed(() => fieldError('participants'))

/** Only blocks the obviously empty case; the API remains the authority. */
const canSubmit = computed(() => title.value.trim().length > 0 && !props.submitting)

function parseParticipants(value: string): string[] {
  return value
    .split(',')
    .map(name => name.trim())
    .filter(name => name.length > 0)
}

function onSubmit() {
  if (!canSubmit.value) return

  const names = parseParticipants(participants.value)

  emit('submit', {
    title: title.value.trim(),
    description: description.value.trim() || null,
    targetDate: targetDate.value || null,
    participants: names.length > 0 ? names : null,
  })
}

/** Called by the page after a successful create. */
function reset() {
  title.value = ''
  description.value = ''
  targetDate.value = ''
  participants.value = ''
}

defineExpose({ reset })
</script>

<template>
  <form
    class="flex flex-col gap-4"
    data-testid="goal-create-form"
    novalidate
    @submit.prevent="onSubmit"
  >
    <UFormField
      label="Title"
      name="title"
      required
      :error="titleError"
      :hint="`${title.length}/${maxTitleLength}`"
    >
      <UInput
        v-model="title"
        placeholder="Walk 8.000 steps a day"
        :maxlength="maxTitleLength"
        autocomplete="off"
        data-testid="goal-title-input"
        class="w-full"
      />
    </UFormField>

    <UFormField
      label="Description"
      name="description"
      :error="descriptionError"
      help="Optional. Visible to everyone taking part."
    >
      <UTextarea
        v-model="description"
        :rows="3"
        :maxlength="maxDescriptionLength"
        placeholder="What does progress look like?"
        data-testid="goal-description-input"
        class="w-full"
      />
    </UFormField>

    <div class="grid gap-4 sm:grid-cols-2">
      <UFormField
        label="Target date"
        name="targetDate"
        help="Optional."
      >
        <UInput
          v-model="targetDate"
          type="date"
          data-testid="goal-target-date-input"
          class="w-full"
        />
      </UFormField>

      <UFormField
        label="Participants"
        name="participants"
        :error="participantsError"
        help="Optional. Separate names with commas."
      >
        <UInput
          v-model="participants"
          placeholder="Robin Sample, Kim Example"
          autocomplete="off"
          data-testid="goal-participants-input"
          class="w-full"
        />
      </UFormField>
    </div>

    <!--
      A non-field error (offline, 5xx) belongs above the button where the user
      is looking after pressing it. aria-live announces it without stealing
      focus.
    -->
    <p
      v-if="error && Object.keys(error.fieldErrors).length === 0"
      class="text-sm text-(--ui-error)"
      role="alert"
      data-testid="goal-create-error"
    >
      {{ error.message }}
    </p>

    <div class="flex justify-end">
      <UButton
        type="submit"
        icon="i-lucide-plus"
        :loading="submitting"
        :disabled="!canSubmit"
        data-testid="goal-submit"
      >
        Create goal
      </UButton>
    </div>
  </form>
</template>
