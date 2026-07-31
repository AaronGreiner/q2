<script setup lang="ts">
/**
 * Signing in.
 *
 * The session is a cookie the server sets, so there is nothing to store here:
 * a successful sign-in updates who the app thinks you are and navigates on.
 *
 * `?next=` carries where somebody was heading before the middleware sent them
 * here, so a link to a chat still opens that chat after signing in.
 */
import { toApiFailure, type ApiFailure } from '~/api/errors'
import { isApiError } from '~/api/errors'

definePageMeta({ layout: 'plain' })

const t = useMessages()
const route = useRoute()
const { public: config } = useRuntimeConfig()
const { login } = useSession()
const toast = useToastMessage()

const email = ref('')
const password = ref('')
const isSubmitting = ref(false)
const failure = ref<ApiFailure | null>(null)

/**
 * Only where a seeded database is what the app is talking to. Both values are
 * empty unless an environment sets them, which is what keeps this off Staging
 * and Production without a second flag to forget.
 */
const demo = computed(() => (config.demoEmail && config.demoPassword
  ? { email: config.demoEmail, password: config.demoPassword }
  : null))

const message = computed(() => signInMessage(failure.value, t.value))

/** Server-reported messages for one field, matched case-insensitively. */
function fieldError(field: string): string | undefined {
  const errors = failure.value?.fieldErrors ?? {}
  const match = Object.keys(errors).find(key => key.toLowerCase() === field.toLowerCase())
  return match ? errors[match]?.[0] : undefined
}

function useDemoAccount() {
  if (!demo.value) return

  email.value = demo.value.email
  password.value = demo.value.password
}

async function onSubmit() {
  if (isSubmitting.value) return

  isSubmitting.value = true
  failure.value = null

  try {
    await login({ email: email.value.trim(), password: password.value })

    // Only ever a path from our own middleware, never an absolute URL: a
    // `next` somebody could point at another host would turn the sign-in
    // screen into an open redirect.
    const next = route.query.next
    const target = typeof next === 'string' && next.startsWith('/') && !next.startsWith('//') ? next : '/'

    await navigateTo(target, { replace: true })
  }
  catch (caught) {
    // A refused sign-in is expected, not an incident: it never reaches Sentry.
    failure.value = isApiError(caught) ? toApiFailure(caught) : null

    if (!failure.value) {
      throw caught
    }
  }
  finally {
    isSubmitting.value = false
  }
}

// Nothing here is worth another visit; a stale sign-in page is a confusing one.
useHead({ title: () => t.value.auth.signInHeading })

void toast
</script>

<template>
  <AuthScreen
    :heading="t.auth.signInHeading"
    :intro="t.auth.signInIntro"
  >
    <form
      class="flex flex-col gap-3.5"
      novalidate
      data-testid="login-form"
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
          data-testid="login-email"
        />
      </UFormField>

      <UFormField
        :label="t.auth.password"
        :error="fieldError('password')"
        name="password"
      >
        <UInput
          v-model="password"
          type="password"
          autocomplete="current-password"
          :placeholder="t.auth.passwordPlaceholder"
          size="xl"
          class="w-full"
          data-testid="login-password"
        />
      </UFormField>

      <p
        v-if="message"
        class="rounded-(--q2-radius-lg) bg-(--ui-error)/10 px-3.5 py-2.5 text-sm font-semibold text-(--ui-error)"
        role="alert"
        data-testid="login-error"
      >
        {{ message }}
      </p>

      <UButton
        type="submit"
        block
        size="xl"
        :loading="isSubmitting"
        class="mt-1 min-h-11 justify-center font-extrabold"
        data-testid="login-submit"
      >
        {{ t.auth.signIn }}
      </UButton>
    </form>

    <p class="mt-4 text-center text-sm text-(--ui-text-muted)">
      {{ t.auth.noAccount }}
      <NuxtLink
        to="/register"
        class="ms-1 font-extrabold text-(--ui-primary) underline-offset-2 hover:underline"
        data-testid="to-register"
      >
        {{ t.auth.toSignUp }}
      </NuxtLink>
    </p>

    <p class="mt-3 text-center text-xs text-(--ui-text-muted)">
      {{ t.auth.noRecovery }}
    </p>

    <section
      v-if="demo"
      class="mt-6 rounded-(--q2-radius-lg) border border-dashed border-(--ui-border) p-3.5"
      aria-labelledby="demo-heading"
      data-testid="demo-account"
    >
      <h2
        id="demo-heading"
        class="text-sm font-extrabold"
      >
        {{ t.auth.demoHeading }}
      </h2>

      <p class="mt-1 text-xs text-(--ui-text-muted)">
        {{ t.auth.demoHint }}
      </p>

      <UButton
        type="button"
        variant="soft"
        block
        size="lg"
        class="mt-2.5 min-h-11 justify-center font-bold"
        data-testid="demo-fill"
        @click="useDemoAccount"
      >
        {{ t.auth.demoFill }}
      </UButton>
    </section>
  </AuthScreen>
</template>
