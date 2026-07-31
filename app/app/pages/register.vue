<script setup lang="ts">
/**
 * Creating an account.
 *
 * Three fields. The handle, the initials and the avatar colour are derived from
 * the name by the server — asking for them here would be three more things to
 * get wrong on a phone keyboard, and none of them is a decision somebody wants
 * to make before they have seen the app.
 */
import { isApiError, toApiFailure, type ApiFailure } from '~/api/errors'

definePageMeta({ layout: 'plain' })

/** Mirrors AccountPolicy.MinimumPasswordLength on the server. */
const minimumPasswordLength = 10

const t = useMessages()
const { register } = useSession()
const toast = useToastMessage()

const name = ref('')
const email = ref('')
const password = ref('')
const isSubmitting = ref(false)
const failure = ref<ApiFailure | null>(null)

const message = computed(() => signInMessage(failure.value, t.value))

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
    const session = await register({
      name: name.value.trim(),
      email: email.value.trim(),
      password: password.value,
    })

    // Registering signs you in, so there is no second form to fill in.
    await navigateTo('/', { replace: true })
    toast.show(t.value.toast.welcome(session.person.displayName))
  }
  catch (caught) {
    failure.value = isApiError(caught) ? toApiFailure(caught) : null

    if (!failure.value) {
      throw caught
    }
  }
  finally {
    isSubmitting.value = false
  }
}

useHead({ title: () => t.value.auth.signUpHeading })
</script>

<template>
  <AuthScreen
    :heading="t.auth.signUpHeading"
    :intro="t.auth.signUpIntro"
  >
    <form
      class="flex flex-col gap-3.5"
      novalidate
      data-testid="register-form"
      @submit.prevent="onSubmit"
    >
      <UFormField
        :label="t.auth.name"
        :error="fieldError('name')"
        name="name"
      >
        <UInput
          v-model="name"
          type="text"
          autocomplete="name"
          :placeholder="t.auth.namePlaceholder"
          size="xl"
          class="w-full"
          data-testid="register-name"
        />
      </UFormField>

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
          data-testid="register-email"
        />
      </UFormField>

      <UFormField
        :label="t.auth.password"
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
          data-testid="register-password"
        />
      </UFormField>

      <p
        v-if="message"
        class="rounded-xl bg-(--ui-error)/10 px-3.5 py-2.5 text-sm font-semibold text-(--ui-error)"
        role="alert"
        data-testid="register-error"
      >
        {{ message }}
      </p>

      <UButton
        type="submit"
        block
        size="xl"
        :loading="isSubmitting"
        class="mt-1 min-h-11 justify-center font-extrabold"
        data-testid="register-submit"
      >
        {{ t.auth.signUp }}
      </UButton>
    </form>

    <p class="mt-4 text-center text-sm text-(--ui-text-muted)">
      {{ t.auth.haveAccount }}
      <NuxtLink
        to="/login"
        class="ms-1 font-extrabold text-(--ui-primary) underline-offset-2 hover:underline"
        data-testid="to-login"
      >
        {{ t.auth.toSignIn }}
      </NuxtLink>
    </p>
  </AuthScreen>
</template>
