<script setup lang="ts">
/**
 * Asking for a way back into an account.
 *
 * One field, and one answer whatever was typed into it: "if there is an
 * account, a mail is on its way". The server answers the same for an address
 * it knows and for one it does not, and this screen must not undo that by
 * saying more.
 *
 * The address is the one typed on the sign-in screen, if there was one
 * (`useAuthEmail`), so tapping "forgot" never asks for it twice.
 */
import { isApiError, toApiFailure, type ApiFailure } from '~/api/errors'

definePageMeta({ layout: 'plain' })

const t = useMessages()
const email = useAuthEmail()
const { requestLink } = usePasswordReset()

const isSubmitting = ref(false)
const isSent = ref(false)
const failure = ref<ApiFailure | null>(null)

/** Not a mistake anybody made: this deployment has no mail account yet. */
const isUnavailable = computed(() =>
  failure.value?.kind === 'unauthorized' && failure.value.reason === 'mailUnavailable')

const message = computed(() => passwordResetMessage(failure.value, t.value))

/** Server-reported messages for one field, matched case-insensitively. */
function fieldError(field: string): string | undefined {
  const errors = failure.value?.fieldErrors ?? {}
  const match = Object.keys(errors).find(key => key.toLowerCase() === field.toLowerCase())
  return match ? errors[match]?.[0] : undefined
}

async function onSubmit() {
  if (isSubmitting.value) return

  isSubmitting.value = true
  failure.value = null

  try {
    await requestLink(email.value.trim())
    isSent.value = true
  }
  catch (caught) {
    // A typo, too many requests, or no mail here: all expected, none of them
    // an incident, so none of them reaches Sentry.
    failure.value = isApiError(caught) ? toApiFailure(caught) : null

    if (!failure.value) {
      throw caught
    }
  }
  finally {
    isSubmitting.value = false
  }
}

/** Back to the form, for somebody whose mail did not come. */
function askAgain() {
  isSent.value = false
  failure.value = null
}

useHead({ title: () => t.value.auth.forgotHeading })
</script>

<template>
  <AuthScreen
    :heading="isSent ? t.auth.forgotSentHeading : t.auth.forgotHeading"
    :intro="isSent ? t.auth.forgotSentIntro : t.auth.forgotIntro"
  >
    <div
      v-if="isSent"
      class="flex flex-col gap-3.5"
      data-testid="forgot-sent"
    >
      <p class="text-center text-sm leading-relaxed text-(--ui-text-muted)">
        {{ t.auth.forgotSentHint }}
      </p>

      <UButton
        type="button"
        color="neutral"
        variant="outline"
        block
        size="xl"
        class="min-h-11 justify-center font-bold"
        data-testid="forgot-again"
        @click="askAgain"
      >
        {{ t.auth.forgotAgain }}
      </UButton>
    </div>

    <form
      v-else
      class="flex flex-col gap-3.5"
      novalidate
      data-testid="forgot-form"
      @submit.prevent="onSubmit"
    >
      <UFormField
        :label="t.auth.email"
        :error="fieldError('email')"
        name="email"
      >
        <UInput
          v-model="email"
          type="email"
          autocomplete="email"
          inputmode="email"
          autocapitalize="none"
          :placeholder="t.auth.emailPlaceholder"
          size="xl"
          class="w-full"
          data-testid="forgot-email"
        />
      </UFormField>

      <p
        v-if="isUnavailable"
        class="rounded-(--q2-radius-lg) border border-dashed border-(--ui-border) px-3.5 py-2.5 text-sm text-(--ui-text-muted)"
        role="status"
        data-testid="forgot-unavailable"
      >
        {{ message }}
      </p>

      <p
        v-else-if="message"
        class="rounded-(--q2-radius-lg) bg-(--ui-error)/10 px-3.5 py-2.5 text-sm font-semibold text-(--ui-error)"
        role="alert"
        data-testid="forgot-error"
      >
        {{ message }}
      </p>

      <UButton
        type="submit"
        block
        size="xl"
        :loading="isSubmitting"
        class="mt-1 min-h-11 justify-center font-extrabold"
        data-testid="forgot-submit"
      >
        {{ t.auth.forgotSubmit }}
      </UButton>
    </form>

    <p class="mt-4 text-center text-sm text-(--ui-text-muted)">
      <template v-if="!isSent">
        {{ t.auth.rememberedIt }}
      </template>
      <NuxtLink
        to="/login"
        class="ms-1 font-extrabold text-(--ui-primary) underline-offset-2 hover:underline"
        data-testid="forgot-to-login"
      >
        {{ t.auth.toSignIn }}
      </NuxtLink>
    </p>
  </AuthScreen>
</template>
