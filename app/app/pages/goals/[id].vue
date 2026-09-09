<script setup lang="ts">
import type { Image } from '~/api/types'
import { pauseLimits } from '~/api/types'

/**
 * One goal: its open window, its team and its record.
 *
 * Loaded on the server so the page is meaningful without JavaScript and a
 * shared link has the right title.
 */
definePageMeta({ layout: 'plain' })

const route = useRoute()
const t = useMessages()

const id = computed(() => String(route.params.id))
const { detail, error, isMissing, isLoading, refresh } = useGoalDetail(id)
const { isDelivering, deliver, maxEdge } = useProofDelivery()
const lifecycle = useGoalLifecycle()

const capturing = ref(false)
const pausing = ref(false)
const closing = ref(false)

async function onDelivered(image: Image) {
  capturing.value = false

  if (await deliver(id.value, image)) await refresh()
}

const goal = computed(() => detail.value?.goal ?? null)
const reminder = computed(() => (goal.value ? formatClock(goal.value.reminderAt) : null))
const currentWindow = computed(() => goal.value?.current ?? null)
const pause = computed(() => goal.value?.pause ?? null)

/**
 * The exits, and who is offered them.
 *
 * Only the owner, and only while the goal is running. The server refuses
 * everything else anyway; this is about not offering a button that always
 * fails.
 */
const canPause = computed(() =>
  Boolean(goal.value?.isMine) && goal.value?.status === 'Active' && !pause.value)

const canClose = computed(() => Boolean(goal.value?.isMine) && goal.value?.status === 'Active')

async function onPause(value: { reason: string, days: number }) {
  if (!await lifecycle.pause(id.value, value.reason, value.days)) return

  pausing.value = false
  await refresh()
}

async function onEndPause() {
  if (await lifecycle.endPause(id.value)) await refresh()
}

async function onVeto() {
  if (await lifecycle.toggleVeto(id.value)) await refresh()
}

async function onClose(completed: boolean) {
  if (!await lifecycle.close(id.value, completed)) return

  closing.value = false
  await refresh()
}

useHead({ title: () => goal.value?.title ?? t.value.goals.detailHeading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader
      :title="t.goals.detailHeading"
      back-to="/goals"
      :back-label="t.common.back"
    />

    <div class="q2-scroll flex-1 px-[18px] pt-1 pb-8">
      <div
        v-if="isLoading"
        class="flex flex-col gap-4"
        aria-busy="true"
        aria-live="polite"
      >
        <span class="sr-only">{{ t.common.loading }}</span>
        <USkeleton class="h-14 w-2/3" />
        <USkeleton class="h-60 w-full rounded-(--q2-radius-lg)" />
      </div>

      <AppErrorState
        v-else-if="error"
        :error="error"
        retryable
        @retry="refresh()"
      />

      <AppStateMessage
        v-else-if="isMissing || !goal || !detail"
        icon="i-lucide-compass"
        :title="t.goals.notFound"
        :description="t.goals.notFoundHint"
        data-testid="goal-not-found"
      >
        <UButton
          to="/goals"
          icon="i-lucide-target"
        >
          {{ t.goals.heading }}
        </UButton>
      </AppStateMessage>

      <article
        v-else
        data-testid="goal-detail"
      >
        <header class="flex items-center gap-3.5">
          <span
            class="flex size-14 shrink-0 items-center justify-center rounded-(--q2-radius-lg) bg-(--ui-bg-accented) text-(--ui-text)"
            aria-hidden="true"
            data-q2-block
          >
            <UIcon
              :name="goalIconName(goal.icon)"
              class="size-7"
            />
          </span>

          <div
            class="min-w-0 flex-1"
            data-q2-private
          >
            <h1 class="text-xl leading-tight font-extrabold text-pretty">
              {{ goal.title }}
            </h1>
            <p class="mt-0.5 text-[13px] font-semibold text-(--ui-text-muted)">
              {{ scheduleLabel(goal.schedule, t) }}
            </p>
          </div>
        </header>

        <p
          v-if="goal.description"
          class="mt-3 text-sm text-(--ui-text-muted)"
          data-q2-private
        >
          {{ goal.description }}
        </p>

        <GoalPauseBanner
          v-if="pause"
          class="mt-4"
          :pause="pause"
          :is-mine="goal.isMine"
          :busy="lifecycle.isBusy.value"
          @end="onEndPause"
          @veto="onVeto"
        />

        <!-- Gone entirely while the goal is set aside: there is no window, so
             a ring would have nothing to show and the status word underneath
             would read "Aktiv" over a goal that is resting. The banner above
             says what is true instead. -->
        <section
          v-if="!pause"
          class="q2-card mt-4 p-5 text-center"
          aria-labelledby="goal-window-heading"
        >
          <h2
            id="goal-window-heading"
            class="sr-only"
          >
            {{ currentWindow ? windowLabel(currentWindow, t) : t.status[goal.status] }}
          </h2>

          <!-- The ring is the open window, not the goal. A goal has no
               percentage any more: it has a period, and a period either was
               delivered or was not. -->
          <AppProgressRing
            v-if="currentWindow"
            class="mx-auto"
            :percent="windowPercent(currentWindow)"
            :size="130"
            :label="windowLabel(currentWindow, t)"
          >
            <span
              class="text-[34px] leading-none font-extrabold"
              data-q2-private
            >{{ currentWindow.confirmedProofs }}/{{ currentWindow.requiredProofs }}</span>
            <span class="mt-0.5 text-[11px] font-bold text-(--ui-text-muted)">{{ windowLabel(currentWindow, t) }}</span>
          </AppProgressRing>

          <p
            v-else
            class="text-[17px] font-extrabold"
          >
            {{ t.status[goal.status] }}
          </p>

          <!--
            The camera, or an explanation of why there is none. The middle state
            is the new one: a photograph is being looked at, and pressing
            anything would only deliver a second the server refuses.
          -->
          <UButton
            v-if="currentWindow?.acceptsProof"
            class="mt-4 w-full justify-center"
            size="xl"
            icon="i-lucide-camera"
            :loading="isDelivering"
            data-testid="goal-deliver-proof"
            @click="capturing = true"
          >
            {{ t.proof.deliver }}
          </UButton>

          <p
            v-else-if="currentWindow?.pendingProofId"
            class="mt-4 flex items-center justify-center gap-2 rounded-(--q2-radius-md) bg-(--ui-bg-elevated) px-3 py-3 text-[13px] font-bold text-(--ui-text-muted)"
            role="status"
            data-testid="goal-proof-waiting"
          >
            <UIcon
              name="i-lucide-hourglass"
              class="size-4"
              aria-hidden="true"
            />
            {{ t.proof.waiting }}
          </p>

          <p
            v-else
            class="mt-4 text-center text-[13px] font-semibold text-(--ui-text-dimmed)"
            data-testid="goal-proof-done"
          >
            {{ t.goals.recordedProof }}
          </p>
        </section>

        <div class="mt-3.5 flex gap-2.5">
          <div class="q2-card flex-1 p-3.5">
            <p class="flex items-center gap-1.5 text-xs font-extrabold text-(--q2-flame-text)">
              <UIcon
                name="i-lucide-flame"
                class="size-4"
                aria-hidden="true"
              />
              {{ t.goals.streak }}
            </p>
            <p
              class="mt-1 text-[22px] font-extrabold"
              data-q2-private
            >
              {{ t.goals.streakDays(goal.streak) }}
            </p>
          </div>

          <!-- The balance, which is the number this product is really about:
               "47 geschafft · 5 verpasst" is a record, where "47" alone would
               be a boast. -->
          <div
            class="q2-card flex-1 p-3.5"
            data-testid="goal-balance"
          >
            <p class="flex items-center gap-1.5 text-xs font-extrabold text-(--ui-text-muted)">
              <UIcon
                name="i-lucide-scale"
                class="size-4"
                aria-hidden="true"
              />
              {{ t.goals.balance }}
            </p>
            <p
              class="mt-1 text-[22px] font-extrabold"
              data-q2-private
            >
              {{ t.goals.balanceValue(goal.windowsDone, goal.windowsMissed) }}
            </p>
            <p class="text-[11px] font-semibold text-(--ui-text-dimmed)">
              {{ t.goals.balanceCaption }}
            </p>
          </div>
        </div>

        <div class="mt-2.5 flex gap-2.5">
          <div class="q2-card flex-1 p-3.5">
            <p class="flex items-center gap-1.5 text-xs font-extrabold text-(--ui-text-muted)">
              <UIcon
                name="i-lucide-bell"
                class="size-4"
                aria-hidden="true"
              />
              {{ t.goals.reminder }}
            </p>
            <p
              class="mt-1 text-lg font-extrabold"
              data-q2-private
            >
              {{ reminder ?? t.goals.noReminder }}
            </p>
          </div>
        </div>

        <div
          v-if="canPause || canClose"
          class="mt-2.5 flex gap-2.5"
          data-testid="goal-exits"
        >
          <UButton
            v-if="canPause"
            class="min-h-11 flex-1 justify-center"
            size="lg"
            color="neutral"
            variant="outline"
            icon="i-lucide-pause"
            :label="t.pause.open"
            :disabled="lifecycle.isBusy.value"
            data-testid="goal-pause-open"
            @click="pausing = true"
          />

          <UButton
            v-if="canClose"
            class="min-h-11 flex-1 justify-center"
            size="lg"
            color="neutral"
            variant="outline"
            icon="i-lucide-archive"
            :label="t.close.open"
            :disabled="lifecycle.isBusy.value"
            data-testid="goal-close-open"
            @click="closing = true"
          />
        </div>

        <section
          class="mt-6"
          aria-labelledby="goal-history-heading"
        >
          <h2
            id="goal-history-heading"
            class="q2-eyebrow mb-3 px-0.5"
          >
            {{ t.goals.historyHeading }}
          </h2>

          <HistoryGrid
            v-if="detail.history.length > 0"
            :history="detail.history"
          />

          <AppStateMessage
            v-else
            icon="i-lucide-calendar"
            :title="t.goals.noHistory"
            :description="t.goals.noHistoryHint"
            data-testid="goal-history-empty"
          />
        </section>

        <section
          v-if="detail.team.length > 0"
          class="mt-6"
          aria-labelledby="goal-team-heading"
        >
          <h2
            id="goal-team-heading"
            class="q2-eyebrow mb-3 px-0.5"
          >
            {{ t.goals.sharedWith }}
          </h2>

          <ul class="flex list-none flex-col gap-2.5 p-0">
            <li
              v-for="member in detail.team"
              :key="member.person.id"
              class="q2-card flex items-center gap-3 px-3 py-3"
              data-testid="goal-team-member"
            >
              <AppAvatar
                :initials="member.person.initials"
                :color="member.person.avatarColor"
                :image-id="member.person.avatarImageId"
                :size="40"
                :online="member.person.isOnline"
              />

              <div
                class="min-w-0 flex-1"
                data-q2-private
              >
                <p class="truncate text-sm font-bold">
                  {{ member.person.displayName }}
                </p>
                <p class="text-[11px] font-semibold text-(--ui-text-muted)">
                  {{ t.friends.streak(member.streak) }}
                </p>
              </div>
            </li>
          </ul>
        </section>
      </article>
    </div>

    <PhotoCapture
      v-model:open="capturing"
      purpose="Proof"
      :max-edge="maxEdge"
      @uploaded="onDelivered"
    />

    <GoalPauseSheet
      v-model:open="pausing"
      :remaining="goal?.remainingPauses ?? 0"
      :max-days="pauseLimits.maxDays"
      :min-reason="pauseLimits.minReason"
      :submitting="lifecycle.isBusy.value"
      :failure="lifecycle.error.value"
      @submit="onPause"
    />

    <GoalCloseSheet
      v-model:open="closing"
      :submitting="lifecycle.isBusy.value"
      @close="onClose"
    />
  </div>
</template>
