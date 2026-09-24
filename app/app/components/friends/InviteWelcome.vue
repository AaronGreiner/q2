<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'
import type { InvitePreview } from '~/api/types'

/**
 * What somebody sees when they open an invite link.
 *
 * Who sent it, and the one next step that turns it into a friendship: an
 * account for somebody who has none, "accept" for somebody signed in. Nothing
 * else — no marketing paragraph between them and the thing they came to do.
 *
 * The sender's name is in the card rather than the heading, because the card
 * is where it can carry `data-q2-private`; the heading belongs to `AuthScreen`,
 * the frame the sign-in and sign-up screens that come next also use.
 *
 * Every state comes from props. Whether the visitor already is a friend, or is
 * looking at their own link, is the server's answer (`relation`), never a guess
 * made here.
 */
const props = defineProps<{
  preview: InvitePreview | null
  failure: ApiFailure | null
  isLoading: boolean
  isSignedIn: boolean
  isAccepting: boolean
  acceptFailure: ApiFailure | null
  /** The sign-up form, carrying the code. */
  registerTo: string
  /** The sign-in form, coming back here afterwards. */
  signInTo: string
}>()

const emit = defineEmits<{ accept: [], retry: [] }>()

const t = useMessages()

const isInvalid = computed(() => props.failure?.kind === 'notFound')

const heading = computed(() => (isInvalid.value ? t.value.join.invalidHeading : t.value.join.heading))
const intro = computed(() => (isInvalid.value ? t.value.join.invalidIntro : t.value.join.intro))

/** What the line under the name says, from where the visitor already stands. */
const standing = computed(() => {
  switch (props.preview?.relation) {
    case 'Self': return t.value.join.ownLink
    case 'Friends': return t.value.join.alreadyFriends
    default: return t.value.join.invites
  }
})

/** Nothing left to do here: it is their own link, or they are friends already. */
const isSettled = computed(() => props.preview?.relation === 'Self' || props.preview?.relation === 'Friends')
</script>

<template>
  <AuthScreen
    :heading="heading"
    :intro="intro"
  >
    <div
      v-if="isLoading"
      class="flex items-center gap-3.5"
      aria-busy="true"
      role="status"
      :aria-label="t.common.loading"
      data-testid="invite-welcome-loading"
    >
      <USkeleton class="size-14 rounded-full" />
      <div class="flex-1 space-y-2">
        <USkeleton class="h-4 w-2/3" />
        <USkeleton class="h-3 w-1/2" />
      </div>
    </div>

    <div
      v-else-if="isInvalid"
      class="flex flex-col gap-2.5"
      data-testid="invite-welcome-invalid"
    >
      <template v-if="isSignedIn">
        <UButton
          to="/"
          block
          size="xl"
          variant="outline"
          color="neutral"
          class="min-h-11 justify-center font-extrabold"
        >
          {{ t.common.toHome }}
        </UButton>
      </template>

      <template v-else>
        <UButton
          to="/register"
          block
          size="xl"
          class="min-h-11 justify-center font-extrabold"
        >
          {{ t.join.createAccount }}
        </UButton>
        <UButton
          to="/login"
          block
          size="xl"
          variant="outline"
          color="neutral"
          class="min-h-11 justify-center font-extrabold"
        >
          {{ t.join.haveAccount }}
        </UButton>
      </template>
    </div>

    <AppErrorState
      v-else-if="failure"
      :error="failure"
      retryable
      @retry="emit('retry')"
    />

    <div
      v-else-if="preview"
      class="flex flex-col gap-5"
      data-testid="invite-welcome"
    >
      <div class="flex items-center gap-3.5">
        <AppAvatar
          :initials="preview.initials"
          :color="preview.avatarColor"
          :size="56"
        />

        <div class="min-w-0 flex-1">
          <p
            class="truncate text-lg font-extrabold"
            data-q2-private
            data-testid="invite-welcome-name"
          >
            {{ preview.displayName }}
          </p>
          <p
            class="mt-0.5 text-sm text-(--ui-text-muted)"
            data-testid="invite-welcome-standing"
          >
            {{ standing }}
          </p>
        </div>
      </div>

      <div class="flex flex-col gap-2.5">
        <template v-if="!isSignedIn">
          <UButton
            :to="registerTo"
            block
            size="xl"
            class="min-h-11 justify-center font-extrabold"
            data-testid="invite-welcome-register"
          >
            {{ t.join.createAccount }}
          </UButton>
          <UButton
            :to="signInTo"
            block
            size="xl"
            variant="outline"
            color="neutral"
            class="min-h-11 justify-center font-extrabold"
            data-testid="invite-welcome-sign-in"
          >
            {{ t.join.haveAccount }}
          </UButton>
        </template>

        <UButton
          v-else-if="isSettled"
          to="/"
          block
          size="xl"
          variant="outline"
          color="neutral"
          class="min-h-11 justify-center font-extrabold"
          data-testid="invite-welcome-home"
        >
          {{ t.common.toHome }}
        </UButton>

        <template v-else>
          <p
            v-if="acceptFailure"
            class="rounded-(--q2-radius-lg) bg-(--ui-error)/10 px-3.5 py-2.5 text-sm font-semibold text-(--ui-error)"
            role="alert"
            data-testid="invite-welcome-error"
          >
            {{ t.errors[acceptFailure.kind] }}
          </p>

          <UButton
            type="button"
            block
            size="xl"
            :loading="isAccepting"
            class="min-h-11 justify-center font-extrabold"
            data-testid="invite-welcome-accept"
            @click="emit('accept')"
          >
            {{ t.join.accept }}
          </UButton>
        </template>
      </div>
    </div>
  </AuthScreen>
</template>
