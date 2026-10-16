<script setup lang="ts">
import type { PushState } from '~/composables/usePushNotifications'

/**
 * Asking for notifications — first in our words, then in the browser's.
 *
 * The browser's prompt can be answered once: "Blockieren" is final, and only
 * the browser's own settings undo it. So it is never the first thing somebody
 * sees. This screen says what they would be told and when it stays quiet, and
 * the prompt only comes from a tap on the button under that
 * (`usePushNotifications.enable`). "Später" asks nothing at all.
 *
 * The page leaves this step out wherever push cannot work — the iOS app, a
 * browser without it, a deployment without keys — so the states here are the
 * three that remain: not asked, on, refused.
 */
const props = defineProps<{
  step: number
  total: number
  state: PushState
  isBusy: boolean
}>()

const emit = defineEmits<{
  back: []
  next: []
  allow: []
}>()

const t = useMessages()

const isDecided = computed(() => props.state === 'on' || props.state === 'blocked')
</script>

<template>
  <OnboardingFrame
    :step="step"
    :total="total"
    :eyebrow="t.onboarding.notifications.eyebrow"
    :heading="t.onboarding.notifications.heading"
    :intro="t.onboarding.notifications.intro"
    :skip-label="isDecided ? null : t.onboarding.later"
    @back="emit('back')"
    @skip="emit('next')"
  >
    <div class="flex flex-col gap-4">
      <!-- What one of them looks like, so "allow" is a decision about something seen. -->
      <div
        class="flex items-start gap-3 rounded-(--q2-radius-xl) bg-(--ui-bg-muted) px-3.5 py-3 shadow-sm"
        aria-hidden="true"
        data-testid="onboarding-notification-sample"
      >
        <span class="flex size-9 shrink-0 items-center justify-center rounded-(--q2-radius-sm) bg-(--ui-text) text-(--ui-bg)">
          <AppMark class="h-4" />
        </span>
        <span class="min-w-0 flex-1">
          <span class="flex items-baseline justify-between gap-2">
            <span class="text-[13px] font-extrabold">{{ t.onboarding.notifications.sampleApp }}</span>
            <span class="text-[11px] font-semibold text-(--ui-text-muted)">{{ t.onboarding.notifications.sampleTime }}</span>
          </span>
          <span class="mt-0.5 block text-[13px] leading-snug">{{ t.onboarding.notifications.sampleBody }}</span>
        </span>
      </div>

      <p
        v-if="state === 'on'"
        class="flex items-start gap-2 text-[13px] leading-relaxed font-semibold"
        role="status"
        data-testid="onboarding-notifications-on"
      >
        <UIcon
          name="i-lucide-bell-ring"
          class="mt-0.5 size-4 shrink-0"
          aria-hidden="true"
        />
        {{ t.onboarding.notifications.on }}
      </p>

      <p
        v-else-if="state === 'blocked'"
        class="flex items-start gap-2 text-[13px] leading-relaxed font-semibold text-(--ui-text-muted)"
        role="status"
        data-testid="onboarding-notifications-blocked"
      >
        <UIcon
          name="i-lucide-bell-off"
          class="mt-0.5 size-4 shrink-0"
          aria-hidden="true"
        />
        {{ t.onboarding.notifications.blocked }}
      </p>
    </div>

    <template #actions>
      <UButton
        v-if="isDecided"
        block
        size="xl"
        trailing-icon="i-lucide-chevron-right"
        class="min-h-12 justify-center font-extrabold"
        data-testid="onboarding-notifications-next"
        @click="emit('next')"
      >
        {{ t.onboarding.next }}
      </UButton>

      <UButton
        v-else
        block
        size="xl"
        icon="i-lucide-bell"
        class="min-h-12 justify-center font-extrabold"
        :loading="isBusy"
        data-testid="onboarding-notifications-allow"
        @click="emit('allow')"
      >
        {{ t.onboarding.notifications.allow }}
      </UButton>
    </template>
  </OnboardingFrame>
</template>
