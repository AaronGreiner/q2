<script setup lang="ts">
import type { HandleCheck, Person } from '~/api/types'

/**
 * Who friends will see: a photograph, a name, and the handle they find you by.
 *
 * The handle is the reason this step exists as much as the photograph is. It
 * was derived from the name at registration and can never change afterwards —
 * it is what friends search for and write down — so this is the one moment its
 * owner sees it and may choose another (#108). The screen says so next to the
 * field, before it is final.
 *
 * Availability is asked while typing, by the page; this component only shows
 * the answer for what is in the field now. An answer for an older value is not
 * shown at all, so a fast typist never reads "Frei" under a handle nobody
 * checked.
 */
export interface ProfileChoice {
  displayName: string
  handle: string
}

const props = defineProps<{
  step: number
  total: number
  person: Person
  handleCheck: HandleCheck | null
  isChecking: boolean
  isSaving: boolean
  /** The last save did not go through. Why is not worth a sentence here. */
  failed: boolean
}>()

const emit = defineEmits<{
  back: []
  capture: []
  check: [handle: string]
  save: [choice: ProfileChoice]
}>()

const t = useMessages()

const maxNameLength = 80

const name = ref(props.person.displayName)
const handle = ref(props.person.handle.replace(/^@/, ''))

/** What the field holds, the way the server would store it. */
const typed = computed(() => {
  const body = handle.value.trim().replace(/^@+/, '').toLowerCase()
  return body ? `@${body}` : ''
})

/** The server's answer, but only if it is about what is in the field now. */
const answer = computed(() => (props.handleCheck && props.handleCheck.handle === typed.value
  ? props.handleCheck.availability
  : typed.value === props.person.handle ? 'Current' : null))

watch(handle, () => {
  if (typed.value && typed.value !== props.person.handle) emit('check', typed.value)
})

const handleError = computed(() => {
  if (!typed.value || answer.value === 'Invalid') return t.value.onboarding.profile.handleInvalid
  if (answer.value === 'Taken') return t.value.onboarding.profile.handleTaken
  return undefined
})

const handleStatus = computed(() => {
  if (handleError.value) return null
  if (props.isChecking || answer.value === null) return { icon: 'i-lucide-loader', text: t.value.onboarding.profile.handleChecking }
  if (answer.value === 'Current') return { icon: 'i-lucide-at-sign', text: t.value.onboarding.profile.handleCurrent }
  return { icon: 'i-lucide-check', text: t.value.onboarding.profile.handleAvailable }
})

const trimmedName = computed(() => name.value.trim())

const canContinue = computed(() => trimmedName.value.length > 0
  && (answer.value === 'Available' || answer.value === 'Current')
  && !props.isChecking
  && !props.isSaving)

function onSubmit() {
  if (!canContinue.value) return
  emit('save', { displayName: trimmedName.value, handle: typed.value })
}

const formId = useId()
</script>

<template>
  <OnboardingFrame
    :step="step"
    :total="total"
    :eyebrow="t.onboarding.profile.eyebrow"
    :heading="t.onboarding.profile.heading"
    :intro="t.onboarding.profile.intro"
    @back="emit('back')"
  >
    <form
      :id="formId"
      class="flex flex-col gap-5"
      novalidate
      data-testid="onboarding-profile-form"
      @submit.prevent="onSubmit"
    >
      <section
        class="flex flex-col items-center gap-3"
        :aria-label="t.profileEdit.photo"
      >
        <AppAvatar
          :initials="person.initials"
          :color="person.avatarColor"
          :image-id="person.avatarImageId"
          :size="96"
        />

        <UButton
          size="lg"
          color="neutral"
          variant="outline"
          icon="i-lucide-camera"
          class="min-h-11"
          :label="person.avatarImageId ? t.onboarding.profile.changePhoto : t.onboarding.profile.addPhoto"
          :disabled="isSaving"
          data-testid="onboarding-photo"
          @click="emit('capture')"
        />

        <p class="text-center text-[13px] font-semibold text-(--ui-text-muted)">
          {{ t.onboarding.profile.photoHint }}
        </p>
      </section>

      <UFormField
        :label="t.onboarding.profile.name"
        :error="trimmedName.length === 0 ? t.profileEdit.nameRequired : undefined"
        name="displayName"
      >
        <UInput
          v-model="name"
          size="xl"
          :placeholder="t.profileEdit.namePlaceholder"
          :maxlength="maxNameLength"
          autocomplete="name"
          class="w-full"
          data-testid="onboarding-name"
        />
      </UFormField>

      <UFormField
        :label="t.onboarding.profile.handle"
        :error="handleError"
        name="handle"
      >
        <UInput
          v-model="handle"
          size="xl"
          autocomplete="off"
          autocapitalize="none"
          autocorrect="off"
          :spellcheck="false"
          :maxlength="40"
          :ui="{ base: 'ps-8' }"
          class="w-full"
          data-testid="onboarding-handle"
          data-q2-private
        >
          <template #leading>
            <span class="text-base font-bold text-(--ui-text-muted)">@</span>
          </template>
        </UInput>

        <template
          v-if="handleStatus"
          #help
        >
          <span
            class="flex items-center gap-1.5 font-semibold"
            :class="handleStatus.icon === 'i-lucide-check' ? 'text-(--ui-text)' : 'text-(--ui-text-muted)'"
            role="status"
            data-testid="onboarding-handle-status"
          >
            <UIcon
              :name="handleStatus.icon"
              class="size-4"
              aria-hidden="true"
            />
            {{ handleStatus.text }}
          </span>
        </template>
      </UFormField>

      <p class="flex items-start gap-2 text-[13px] leading-relaxed font-semibold text-(--ui-text-muted)">
        <UIcon
          name="i-lucide-lock"
          class="mt-0.5 size-4 shrink-0"
          aria-hidden="true"
        />
        {{ t.onboarding.profile.handleHint }}
      </p>

      <p
        v-if="failed"
        class="rounded-(--q2-radius-lg) bg-(--ui-error)/10 px-3.5 py-2.5 text-sm font-semibold text-(--ui-error)"
        role="alert"
        data-testid="onboarding-profile-error"
      >
        {{ t.onboarding.profile.saveFailed }}
      </p>
    </form>

    <template #actions>
      <UButton
        type="submit"
        :form="formId"
        block
        size="xl"
        trailing-icon="i-lucide-chevron-right"
        class="min-h-12 justify-center font-extrabold"
        :loading="isSaving"
        :disabled="!canContinue"
        data-testid="onboarding-profile-next"
      >
        {{ t.onboarding.next }}
      </UButton>
    </template>
  </OnboardingFrame>
</template>
