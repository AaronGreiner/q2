<script setup lang="ts">
import { isApiError } from '~/api/errors'

/**
 * The way out that does not lead back.
 *
 * It says what goes before it asks for anything, and it names the one thing
 * that does *not* go — everybody else's half of a group conversation — because
 * that is the part people are surprised by afterwards.
 *
 * The password is asked for again, and that is a rule rather than friction: a
 * session cookie authorises reading somebody's screens, not erasing their year
 * from a borrowed phone. There is deliberately no "type DELETE to confirm"
 * ritual on top of it; a real credential is a better gate than a word somebody
 * copies without reading.
 */
const open = defineModel<boolean>('open', { required: true })

const t = useMessages()
const api = useQ2Api()
const { report } = useErrorReporter()

const password = ref('')
const message = ref<string | null>(null)
const isDeleting = ref(false)

watch(open, (isOpen) => {
  if (isOpen) return

  // Never left in memory behind a closed sheet.
  password.value = ''
  message.value = null
})

async function confirm() {
  if (!password.value || isDeleting.value) return

  isDeleting.value = true
  message.value = null

  try {
    await api.accounts.remove(password.value)

    /*
     * A full page load rather than a route change.
     *
     * The account is gone and so is the session, so every piece of state this
     * app is holding — the cached profile, the async-data payloads, the
     * language read from settings — belongs to somebody who no longer exists.
     * Reloading is the only way to be sure none of it is still on screen.
     */
    if (import.meta.client) window.location.href = '/login'
  }
  catch (caught) {
    isDeleting.value = false

    // A wrong password is the person's own answer, not a defect: it gets its
    // own sentence rather than a Sentry issue.
    if (isApiError(caught) && caught.status === 401) {
      message.value = t.value.deleteAccount.wrongPassword
      return
    }

    report(caught, { feature: 'account', action: 'delete' })
    message.value = t.value.deleteAccount.failed
  }
}
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="t.deleteAccount.heading"
    :description="t.deleteAccount.body"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <div class="flex flex-col gap-4 pb-2">
        <UFormField
          :label="t.deleteAccount.password"
          :hint="t.deleteAccount.passwordHint"
        >
          <UInput
            v-model="password"
            type="password"
            autocomplete="current-password"
            class="w-full"
            data-testid="delete-account-password"
          />
        </UFormField>

        <p
          v-if="message"
          class="text-[13px] font-semibold text-(--ui-error)"
          role="alert"
          data-testid="delete-account-message"
        >
          {{ message }}
        </p>

        <UButton
          block
          size="xl"
          color="error"
          icon="i-lucide-trash-2"
          :label="t.deleteAccount.confirm"
          :disabled="!password || isDeleting"
          :loading="isDeleting"
          data-testid="delete-account-confirm"
          @click="confirm"
        />
      </div>
    </template>
  </UDrawer>
</template>
