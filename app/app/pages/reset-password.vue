<script setup lang="ts">
/**
 * Setting a new password with the link from a reset mail.
 *
 * The token arrives in the fragment of the address, and an inline script lifts
 * it out before anything else on the page runs — utils/resetLink.ts says why
 * that has to happen that early. By the time this component mounts, the
 * address bar says `/reset-password` and nothing more.
 *
 * Reachable with or without a session (middleware/auth.global.ts): a link is
 * opened on whatever device the mail was read on. Either way a reset ends every
 * session of the account, this one included, so the screen ends on the way to
 * the sign-in, with the address already filled in there.
 */
import { isApiError, toApiFailure, type ApiFailure } from '~/api/errors'

definePageMeta({ layout: 'plain' })

/** Mirrors AccountPolicy.MinimumPasswordLength on the server. */
const minimumPasswordLength = 10

const t = useMessages()
const email = useAuthEmail()
const { reset } = usePasswordReset()

/**
 * Undefined until the page has mounted, null when the address carried none.
 * The token only ever exists in the browser, so the server renders the form
 * and the browser decides.
 */
const token = ref<string | null | undefined>(undefined)
const password = ref('')
const isSubmitting = ref(false)
const isDone = ref(false)
const failure = ref<ApiFailure | null>(null)

const isInvalid = computed(() => token.value === null || isRefusedResetLink(failure.value))
const message = computed(() => passwordResetMessage(failure.value, t.value))

const heading = computed(() => {
  if (isDone.value) return t.value.auth.resetDoneHeading
  return isInvalid.value ? t.value.auth.resetInvalidHeading : t.value.auth.resetHeading
})

const intro = computed(() => {
  if (isDone.value) return t.value.auth.resetDoneIntro
  return isInvalid.value ? t.value.auth.resetInvalid : t.value.auth.resetIntro
})

/** Server-reported messages for one field, matched case-insensitively. */
function fieldError(field: string): string | undefined {
  const errors = failure.value?.fieldErrors ?? {}
  const match = Object.keys(errors).find(key => key.toLowerCase() === field.toLowerCase())
  return match ? errors[match]?.[0] : undefined
}

async function onSubmit() {
  if (isSubmitting.value || !token.value) return

  isSubmitting.value = true
  failure.value = null

  try {
    const result = await reset(token.value, password.value)

    // For the sign-in screen, which is where this ends.
    email.value = result.email
    password.value = ''
    isDone.value = true
  }
  catch (caught) {
    // A refused link or a short password is ordinary, not an incident.
    failure.value = isApiError(caught) ? toApiFailure(caught) : null

    if (!failure.value) {
      throw caught
    }
  }
  finally {
    isSubmitting.value = false
  }
}

/**
 * A link opened while this page is already showing changes only the fragment,
 * which the browser treats as the same page: no reload, no inline script and no
 * new mount. So the page notices by itself — and takes the token out of the
 * address bar just as quickly.
 */
function onHashChange() {
  const next = takeResetToken()

  if (next) {
    token.value = next
    failure.value = null
    isDone.value = false
  }
}

onMounted(() => {
  token.value = takeResetToken()
  window.addEventListener('hashchange', onHashChange)
})

onBeforeUnmount(() => {
  window.removeEventListener('hashchange', onHashChange)
})

useHead({
  title: () => heading.value,
  script: [
    {
      key: 'q2-lift-reset-token',
      innerHTML: liftResetTokenScript,
      tagPosition: 'head',
      tagPriority: 'critical',
    },
  ],
})
</script>

<template>
  <AuthScreen
    :heading="heading"
    :intro="intro"
  >
    <UButton
      v-if="isDone"
      to="/login"
      block
      size="xl"
      class="min-h-11 justify-center font-extrabold"
      data-testid="reset-to-login"
    >
      {{ t.auth.toSignIn }}
    </UButton>

    <UButton
      v-else-if="isInvalid"
      to="/forgot-password"
      block
      size="xl"
      class="min-h-11 justify-center font-extrabold"
      data-testid="reset-invalid"
    >
      {{ t.auth.forgotAgain }}
    </UButton>

    <form
      v-else
      class="flex flex-col gap-3.5"
      novalidate
      data-testid="reset-form"
      @submit.prevent="onSubmit"
    >
      <UFormField
        :label="t.auth.newPassword"
        :error="fieldError('password')"
        :help="t.auth.passwordHint(minimumPasswordLength)"
        name="password"
      >
        <UInput
          v-model="password"
          type="password"
          autocomplete="new-password"
          :placeholder="t.auth.passwordPlaceholder"
          size="xl"
          class="w-full"
          data-testid="reset-password"
        />
      </UFormField>

      <p
        v-if="message"
        class="rounded-(--q2-radius-lg) bg-(--ui-error)/10 px-3.5 py-2.5 text-sm font-semibold text-(--ui-error)"
        role="alert"
        data-testid="reset-error"
      >
        {{ message }}
      </p>

      <UButton
        type="submit"
        block
        size="xl"
        :loading="isSubmitting"
        class="mt-1 min-h-11 justify-center font-extrabold"
        data-testid="reset-submit"
      >
        {{ t.auth.resetSubmit }}
      </UButton>
    </form>
  </AuthScreen>
</template>
