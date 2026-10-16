<script setup lang="ts">
import type { GoalDraft } from '~/api/types'

/**
 * The link, sent for something.
 *
 * Right after the first Do on purpose: the waiting Do is on this screen, and
 * it starts the moment somebody comes through the link — a reason to send it
 * that "invite your friends" on its own never was. Somebody who already has
 * friends is told who, and offered the link for more.
 *
 * The share sheet is the main button until the link has gone somewhere; then
 * "Weiter" takes its place and sharing again stays one tap away, and the
 * waiting Do keeps waiting. The link is also shown as text, for a browser that
 * refuses the clipboard.
 *
 * Somebody who does not want to invite anybody yet is not made to: "Erstmal
 * ohne Freunde" ends the introduction here, and the waiting Do lapses — after
 * a question, because it is something they wrote. Without a Do there is
 * nothing to lose and nothing to ask. Once the link has gone out, or there is
 * a friend, the way out is not needed and not shown.
 */
const props = defineProps<{
  step: number
  total: number
  url: string
  isLoading: boolean
  friendNames: string[]
  draft: GoalDraft | null
  hasShared: boolean
  /** Leaving without friends is under way. */
  isLeaving?: boolean
  /** Leaving without friends did not go through. */
  leaveFailed?: boolean
}>()

const emit = defineEmits<{
  back: []
  next: []
  share: []
  copy: []
  /** End the introduction without friends; the waiting Do lapses. */
  leave: []
}>()

const t = useMessages()

const hasFriends = computed(() => props.friendNames.length > 0)

/** "Lena", "Lena und Jonas", "Lena, Jonas und 2 weiteren" — the list that says who. */
const names = computed(() => {
  const all = props.friendNames
  const words = t.value.onboarding.invite

  return all.length <= 2
    ? all.join(` ${words.and} `)
    : `${all[0]}, ${all[1]} ${words.and} ${words.others(all.length - 2)}`
})

const canMoveOn = computed(() => props.hasShared || hasFriends.value)

const confirmingLeave = ref(false)

function onLeave() {
  if (props.draft) confirmingLeave.value = true
  else emit('leave')
}
</script>

<template>
  <OnboardingFrame
    :step="step"
    :total="total"
    :eyebrow="t.onboarding.invite.eyebrow"
    :heading="hasFriends ? t.onboarding.invite.headingFriends : t.onboarding.invite.headingAlone"
    :intro="hasFriends ? t.onboarding.invite.introFriends(names) : t.onboarding.invite.introAlone"
    @back="emit('back')"
  >
    <div class="flex flex-col gap-4">
      <GoalDraftCard
        v-if="draft"
        :draft="draft"
        :actions="false"
      />

      <div>
        <p
          v-if="url"
          class="q2-selectable truncate rounded-(--q2-radius-md) bg-(--ui-bg-muted) px-3.5 py-3 text-[13px] font-semibold text-(--ui-text-muted)"
          data-testid="onboarding-invite-link"
          data-q2-block
        >
          {{ url }}
        </p>
        <USkeleton
          v-else-if="isLoading"
          class="h-11 w-full rounded-(--q2-radius-md)"
        />

        <UButton
          class="mt-2 min-h-11"
          size="lg"
          color="neutral"
          variant="ghost"
          icon="i-lucide-copy"
          :label="t.onboarding.invite.copy"
          :disabled="!url"
          data-testid="onboarding-invite-copy"
          @click="emit('copy')"
        />
      </div>

      <p
        v-if="hasShared"
        class="flex items-start gap-2 text-[13px] leading-relaxed font-semibold"
        role="status"
        data-testid="onboarding-invite-sent"
      >
        <UIcon
          name="i-lucide-check"
          class="mt-0.5 size-4 shrink-0"
          aria-hidden="true"
        />
        {{ t.onboarding.invite.sent }}
      </p>

      <p
        v-if="leaveFailed"
        class="rounded-(--q2-radius-lg) bg-(--ui-error)/10 px-3.5 py-2.5 text-sm font-semibold text-(--ui-error)"
        role="alert"
        data-testid="onboarding-invite-leave-error"
      >
        {{ t.onboarding.invite.leaveFailed }}
      </p>
    </div>

    <template #actions>
      <UButton
        v-if="canMoveOn"
        block
        size="xl"
        trailing-icon="i-lucide-chevron-right"
        class="min-h-12 justify-center font-extrabold"
        data-testid="onboarding-invite-next"
        @click="emit('next')"
      >
        {{ t.onboarding.next }}
      </UButton>

      <UButton
        block
        size="xl"
        icon="i-lucide-share-2"
        :color="canMoveOn ? 'neutral' : 'primary'"
        :variant="canMoveOn ? 'outline' : 'solid'"
        class="min-h-12 justify-center font-extrabold"
        :disabled="!url"
        data-testid="onboarding-invite-share"
        @click="emit('share')"
      >
        {{ t.onboarding.invite.share }}
      </UButton>

      <UButton
        v-if="!canMoveOn"
        block
        size="lg"
        color="neutral"
        variant="ghost"
        class="min-h-11 justify-center font-bold text-(--ui-text-muted)"
        :loading="isLeaving"
        data-testid="onboarding-invite-leave"
        @click="onLeave"
      >
        {{ t.onboarding.invite.leave }}
      </UButton>
    </template>

    <AppConfirmDialog
      v-if="draft"
      v-model:open="confirmingLeave"
      :title="t.onboarding.invite.leaveTitle"
      :description="t.onboarding.invite.leaveBody(draft.title)"
      :confirm-label="t.onboarding.invite.leaveConfirm"
      private-description
      @confirm="emit('leave')"
    />
  </OnboardingFrame>
</template>
