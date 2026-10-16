<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'
import type { Friend, GoalDraft } from '~/api/types'
import type { DraftFormValue } from '~/components/goals/GoalDraftForm.vue'

/**
 * The first promise, made during the introduction.
 *
 * The step that used to be a wall (#93): a new account has nobody to make a
 * goal in front of, and was told to invite somebody first. Now it makes the
 * Do anyway, and the Do waits — it starts by itself the moment the first
 * friend arrives (docs/adr/0039-first-run-onboarding.md). Somebody who came
 * through an invite link already has a friend, and gets an ordinary goal with
 * that friend checking it.
 *
 * Making a commitment before being asked to invite anybody is deliberate: the
 * next step's link is then sent *for* something, which is what makes it worth
 * sending.
 *
 * Somebody whose Dos are already running — an existing account meeting the
 * introduction once — is not asked for another one.
 */
const props = defineProps<{
  step: number
  total: number
  friends: Friend[]
  draft: GoalDraft | null
  ownGoalCount: number
  isSaving: boolean
  error: ApiFailure | null
}>()

const emit = defineEmits<{
  back: []
  skip: []
  next: []
  submit: [value: DraftFormValue]
}>()

const t = useMessages()

const formId = useId()
const valid = ref(false)

const isRunning = computed(() => props.ownGoalCount > 0)
const hasFriends = computed(() => props.friends.length > 0)

const heading = computed(() => (isRunning.value ? t.value.onboarding.firstDo.runningHeading : t.value.onboarding.firstDo.heading))

const intro = computed(() => {
  if (isRunning.value) return t.value.onboarding.firstDo.runningIntro(props.ownGoalCount)
  return hasFriends.value ? t.value.onboarding.firstDo.introWithFriends : t.value.onboarding.firstDo.introWaiting
})
</script>

<template>
  <OnboardingFrame
    :step="step"
    :total="total"
    :eyebrow="t.onboarding.firstDo.eyebrow"
    :heading="heading"
    :intro="intro"
    :skip-label="isRunning ? null : t.onboarding.later"
    @back="emit('back')"
    @skip="emit('skip')"
  >
    <AppStateMessage
      v-if="isRunning"
      icon="i-lucide-list-checks"
      :title="t.tour.dos.title"
      :description="t.tour.dos.body"
      data-testid="onboarding-first-do-running"
    />

    <GoalDraftForm
      v-else
      v-model:valid="valid"
      :form-id="formId"
      :friends="friends"
      :initial="draft"
      :error="error"
      @submit="value => emit('submit', value)"
    />

    <template #actions>
      <UButton
        v-if="isRunning"
        block
        size="xl"
        trailing-icon="i-lucide-chevron-right"
        class="min-h-12 justify-center font-extrabold"
        data-testid="onboarding-first-do-next"
        @click="emit('next')"
      >
        {{ t.onboarding.next }}
      </UButton>

      <UButton
        v-else
        type="submit"
        :form="formId"
        block
        size="xl"
        :icon="hasFriends ? 'i-lucide-sparkles' : 'i-lucide-hourglass'"
        class="min-h-12 justify-center font-extrabold"
        :loading="isSaving"
        :disabled="!valid || isSaving"
        data-testid="onboarding-first-do-submit"
      >
        {{ hasFriends ? t.onboarding.firstDo.submitNow : t.onboarding.firstDo.submitWaiting }}
      </UButton>
    </template>
  </OnboardingFrame>
</template>
