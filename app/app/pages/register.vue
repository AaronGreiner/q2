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

const route = useRoute()
const toast = useToastMessage()

/*
 * The code somebody arrived with, if they came from an invite link's page.
 *
 * In the address rather than in state: `/join/<code>` is server-rendered, and
 * state set there did not survive the redirect to this form — which is why no
 * link ever made a friendship. An address survives a reload as well. Sentry
 * drops query strings, and the code was in the address on the page before
 * anyway.
 */
const inviteCode = typeof route.query.invite === 'string' && route.query.invite ? route.query.invite : null

/*
 * Whose link it is, so the form can say who they will be friends with. Only
 * read when there is a code; a stale one simply shows nothing, and the server
 * ignores it at registration rather than refusing the account.
 */
const invite = inviteCode ? useInviteLink(inviteCode) : null
const inviter = computed(() => invite?.preview.value ?? null)

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

  // Read before registering: signing up clears every cached read, the preview
  // of whose link this is included.
  const befriends = inviter.value !== null

  try {
    await register({
      name: name.value.trim(),
      email: email.value.trim(),
      password: password.value,

      // A code that no longer means anything is ignored by the server rather
      // than refused: registration is the worst moment to fail over a stale
      // link somebody was forwarded.
      inviteCode: inviteCode ?? undefined,
    })

    // The start screen does not show a new friendship, so this is the one
    // place that says the link did what it promised.
    if (befriends) {
      toast.show(t.value.toast.befriended)
    }

    // Registering signs you in, so there is no second form to fill in.
    await navigateTo('/', { replace: true })
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

/*
 * Somebody who arrived through a link and turns out to have an account goes
 * back to the link's page after signing in, where they can accept it.
 */
const signInTo = inviteCode ? `/login?next=${encodeURIComponent(`/join/${encodeURIComponent(inviteCode)}`)}` : '/login'

useHead({ title: () => t.value.auth.signUpHeading })
</script>

<template>
  <AuthScreen
    :heading="t.auth.signUpHeading"
    :intro="t.auth.signUpIntro"
  >
    <div
      v-if="inviter"
      class="mb-5 flex items-center gap-3 rounded-(--q2-radius-lg) bg-(--ui-bg-muted) p-3"
      data-testid="register-invite"
    >
      <AppAvatar
        :initials="inviter.initials"
        :color="inviter.avatarColor"
        :size="40"
      />
      <div class="min-w-0 flex-1">
        <p
          class="truncate text-sm font-extrabold"
          data-q2-private
        >
          {{ inviter.displayName }}
        </p>
        <p class="text-xs text-(--ui-text-muted)">
          {{ t.join.registerHint }}
        </p>
      </div>
    </div>

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
        class="rounded-(--q2-radius-lg) bg-(--ui-error)/10 px-3.5 py-2.5 text-sm font-semibold text-(--ui-error)"
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
        :to="signInTo"
        class="ms-1 font-extrabold text-(--ui-primary) underline-offset-2 hover:underline"
        data-testid="to-login"
      >
        {{ t.auth.toSignIn }}
      </NuxtLink>
    </p>
  </AuthScreen>
</template>
