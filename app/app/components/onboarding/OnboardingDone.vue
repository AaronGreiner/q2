<script setup lang="ts">
/**
 * The last step: what is now set up, and the way into the app.
 *
 * A list of facts rather than a celebration — the first real thing to be
 * pleased about is a kept window, and the streak is where that happens. Each
 * line is a state, so it is drawn in the text colour with a tick or a quiet
 * dash, never in the accent; the accent is the one button, because it is the
 * one thing to do.
 *
 * Pressing it is what tells the server the introduction is over. A failure
 * stays here and says so, rather than letting somebody into an app that will
 * send them straight back.
 */
export interface OnboardingSummary {
  hasPhoto: boolean
  handle: string
  firstDo: 'running' | 'waiting' | 'none'
  friendCount: number
  notifications: boolean | null
}

const props = defineProps<{
  step: number
  total: number
  summary: OnboardingSummary
  isFinishing: boolean
  failed: boolean
}>()

const emit = defineEmits<{ back: [], finish: [] }>()

const t = useMessages()

const rows = computed(() => {
  const done = t.value.onboarding.done
  const summary = props.summary

  const all = [
    { key: 'photo', label: done.photo, value: summary.hasPhoto ? done.photoSet : done.photoMissing, ok: summary.hasPhoto, isPrivate: false },
    { key: 'handle', label: done.handle, value: summary.handle, ok: true, isPrivate: true },
    {
      key: 'firstDo',
      label: done.firstDo,
      value: summary.firstDo === 'running' ? done.firstDoRunning : summary.firstDo === 'waiting' ? done.firstDoWaiting : done.firstDoNone,
      ok: summary.firstDo !== 'none',
      isPrivate: false,
    },
    {
      key: 'friends',
      label: done.friends,
      value: summary.friendCount > 0 ? done.friendsCount(summary.friendCount) : done.friendsNone,
      ok: summary.friendCount > 0,
      isPrivate: false,
    },
  ]

  // Only where it was asked: a device that cannot be notified has nothing to report.
  if (summary.notifications !== null) {
    all.push({
      key: 'notifications',
      label: done.notifications,
      value: summary.notifications ? done.notificationsOn : done.notificationsOff,
      ok: summary.notifications,
      isPrivate: false,
    })
  }

  return all
})
</script>

<template>
  <OnboardingFrame
    :step="step"
    :total="total"
    :eyebrow="t.onboarding.done.eyebrow"
    :heading="t.onboarding.done.heading"
    :intro="t.onboarding.done.intro"
    @back="emit('back')"
  >
    <dl
      class="flex flex-col divide-y divide-(--ui-border-muted) rounded-(--q2-radius-lg) border border-(--ui-border) px-4"
      data-testid="onboarding-summary"
    >
      <div
        v-for="row in rows"
        :key="row.key"
        class="flex items-center gap-3 py-3"
        :data-testid="`onboarding-summary-${row.key}`"
      >
        <UIcon
          :name="row.ok ? 'i-lucide-circle-check' : 'i-lucide-circle-dashed'"
          class="size-5 shrink-0"
          :class="row.ok ? 'text-(--ui-text)' : 'text-(--ui-text-dimmed)'"
          aria-hidden="true"
        />
        <dt class="shrink-0 text-sm font-semibold text-(--ui-text-muted)">
          {{ row.label }}
        </dt>
        <dd
          class="min-w-0 flex-1 text-end text-sm font-bold [overflow-wrap:anywhere]"
          :data-q2-private="row.isPrivate ? '' : undefined"
        >
          {{ row.value }}
        </dd>
      </div>
    </dl>

    <p
      v-if="failed"
      class="mt-4 rounded-(--q2-radius-lg) bg-(--ui-error)/10 px-3.5 py-2.5 text-sm font-semibold text-(--ui-error)"
      role="alert"
      data-testid="onboarding-done-error"
    >
      {{ t.onboarding.done.failed }}
    </p>

    <template #actions>
      <UButton
        block
        size="xl"
        trailing-icon="i-lucide-arrow-right"
        class="min-h-12 justify-center font-extrabold"
        :loading="isFinishing"
        data-testid="onboarding-finish"
        @click="emit('finish')"
      >
        {{ t.onboarding.done.start }}
      </UButton>
    </template>
  </OnboardingFrame>
</template>
