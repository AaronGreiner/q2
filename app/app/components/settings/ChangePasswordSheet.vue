<script setup lang="ts">
import { isApiError, toApiFailure, type ApiFailure } from '~/api/errors'

/**
 * A new password, while signed in (#40).
 *
 * The current password first, and that is a rule rather than friction: an
 * open session is enough to read somebody's screens, not to lock them out of
 * their own account from a borrowed phone. A wrong one is the person's own
 * answer and is said under its field, from the code the server sends; so is
 * the lockout after too many.
 *
 * This device stays signed in, every other one is signed out, and the sheet
 * says so before anything is typed — that is the consequence people need to
 * know beforehand, not the one they find out on the laptop later.
 */
const open = defineModel<boolean>('open', { required: true })

/** Mirrors AccountPolicy.MinimumPasswordLength on the server, for the hint only. */
const minimumPasswordLength = 10

const t = useMessages()
const { changePassword } = useSession()
const { report } = useErrorReporter()
const toast = useToastMessage()

const current = ref('')
const next = ref('')
const isSaving = ref(false)
const failure = ref<ApiFailure | null>(null)
const message = ref<string | null>(null)

watch(open, (isOpen) => {
  if (isOpen) return

  // Never left in memory behind a closed sheet.
  current.value = ''
  next.value = ''
  failure.value = null
  message.value = null
})

/** What is wrong with one field, worded in the reader's language from the server's code. */
function fieldError(field: string): string | undefined {
  return fieldErrorText(failure.value, field, t.value)
}

async function save() {
  if (!current.value || !next.value || isSaving.value) return

  isSaving.value = true
  failure.value = null
  message.value = null

  try {
    await changePassword(current.value, next.value)
    toast.show(t.value.toast.passwordChanged)
    open.value = false
  }
  catch (caught) {
    // A wrong or too-short password is an answer about a field, not a defect.
    if (isApiError(caught) && caught.kind === 'validation') {
      failure.value = toApiFailure(caught)
      return
    }

    report(caught, { feature: 'account', action: 'changePassword' })
    message.value = t.value.passwordChange.failed
  }
  finally {
    isSaving.value = false
  }
}
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="t.passwordChange.heading"
    :description="t.passwordChange.body"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <form
        class="flex flex-col gap-4 pb-2"
        novalidate
        data-testid="change-password-form"
        @submit.prevent="save"
      >
        <UFormField
          :label="t.passwordChange.current"
          :error="fieldError('currentPassword')"
          name="currentPassword"
        >
          <AppPasswordInput
            v-model="current"
            autocomplete="current-password"
            class="w-full"
            data-testid="change-password-current"
          />
        </UFormField>

        <UFormField
          :label="t.passwordChange.next"
          :error="fieldError('newPassword')"
          :help="t.auth.passwordHint(minimumPasswordLength)"
          name="newPassword"
        >
          <AppPasswordInput
            v-model="next"
            autocomplete="new-password"
            class="w-full"
            data-testid="change-password-new"
          />
        </UFormField>

        <p
          v-if="message"
          class="text-[13px] font-semibold text-(--ui-error)"
          role="alert"
          data-testid="change-password-message"
        >
          {{ message }}
        </p>

        <UButton
          type="submit"
          block
          size="xl"
          icon="i-lucide-key-round"
          :label="t.passwordChange.submit"
          :disabled="!current || !next || isSaving"
          :loading="isSaving"
          data-testid="change-password-submit"
        />
      </form>
    </template>
  </UDrawer>
</template>
