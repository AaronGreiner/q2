<script setup lang="ts">
import type { EditGoalRequest } from '~/api/types'
import type { Captured } from '~/utils/captured'
import { pauseLimits } from '~/api/types'
import type { PauseRequestValue } from '~/components/goals/GoalPauseSheet.vue'

/**
 * One goal: its open window, its team and its record.
 *
 * Loaded on the server so the page is meaningful without JavaScript and a
 * shared link has the right title.
 */
definePageMeta({ layout: 'plain', middleware: ['goal-photo'] })

const route = useRoute()
const t = useMessages()

const id = computed(() => String(route.params.id))
const { detail, error, isMissing, isLoading, refresh } = useGoalDetail(id)
const { isDelivering, deliver, answerSpotCheck, maxEdge } = useProofDelivery()
const lifecycle = useGoalLifecycle()
const dontUse = useDontUse(() => refresh())
const now = useNow()
const zone = useTimeZoneOffset()

/** The reader's day, for a Don't's run. */
const today = computed(() => localDay(now.value, zone.value))

/*
 * Asked to show one of this goal's photographs (#112). A goal with a
 * conversation never gets here with `?photo=` — `middleware/goal-photo.ts`
 * hands that on to the thread — so this is a goal with none, and the
 * photograph opens full screen over it. Once: closing it leaves the goal.
 */
const proofViewer = useProofViewer()
let shownPhoto: string | null = null

watch([() => route.query.photo, detail], ([photo, loaded]) => {
  if (!import.meta.client || typeof photo !== 'string' || !loaded || shownPhoto === photo) return

  shownPhoto = photo
  void proofViewer.show(photo, loaded.goal.title)
}, { immediate: true })

const capturing = ref(false)

/** The camera for a use's optional photograph — a different hand-in from a proof's. */
const capturingUse = ref(false)

/** The live-only camera for an open spot check (docs/adr/0038-spot-checks.md). */
const answering = ref<string | null>(null)

async function handInCheck(captured: Captured) {
  const checkId = answering.value
  if (!checkId) return null

  const result = await answerSpotCheck(id.value, checkId, captured)
  await refresh()

  return result.status === 'refused' ? result.message : null
}
const pausing = ref(false)
const closing = ref(false)
const reminding = ref(false)
const editing = ref(false)

/** Who could be added as a checker — the same read the goals list makes for its create sheet. */
const { friends } = useFriends()

/** Moves on to the goal's conversation when it has one — see useProofDelivery. */
async function handIn(captured: Captured) {
  const result = await deliver(id.value, captured)
  if (result.status !== 'moved') await refresh()

  return result.status === 'refused' ? result.message : null
}

async function onLog() {
  if (await dontUse.logUse(id.value)) await refresh()
}

/** A use with its photograph: logged once the picture is up, refused on the camera's screen. */
async function handInUse(captured: Captured) {
  const logged = await dontUse.logUse(id.value, captured.image.id)
  if (!logged) return t.value.proof.refusedFailed

  await refresh()
  return null
}

const goal = computed(() => detail.value?.goal ?? null)
const reminder = computed(() => (goal.value ? formatClock(goal.value.reminderAt) : null))
const currentWindow = computed(() => goal.value?.current ?? null)
const pause = computed(() => goal.value?.pause ?? null)

/** A pause its owner planned ahead (#99): theirs to see and to take back. */
const plannedPause = computed(() => goal.value?.plannedPause ?? null)
const toast = useToastMessage()

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

/** Only the owner changes the terms, and only while the goal runs (docs/adr/0044). */
const canEdit = computed(() => Boolean(goal.value?.isMine) && goal.value?.status === 'Active')

const pendingChange = computed(() => goal.value?.pendingChange ?? null)

/**
 * Only the owner is reminded, so only the owner moves it — and only while it
 * runs. A Don't has no daily reminder at all (docs/adr/0037-donts.md): there
 * is nothing to be reminded to do (#89).
 */
const canEditReminder = computed(() =>
  Boolean(goal.value?.isMine) && goal.value?.status === 'Active' && goal.value?.kind !== 'Dont')

/**
 * The exits, behind the ellipsis in the header. They were two buttons as large
 * as the camera, for things done once in a goal's life; stopping is final.
 */
const exits = computed(() => [
  ...(canEdit.value
    ? [{ label: t.value.edit.open, icon: 'i-lucide-pencil', onSelect: () => openEdit() }]
    : []),
  ...(canPause.value
    ? [{ label: t.value.pause.open, icon: 'i-lucide-pause', onSelect: () => { pausing.value = true } }]
    : []),
  ...(canClose.value
    ? [{ label: t.value.close.open, icon: 'i-lucide-archive', onSelect: () => { closing.value = true } }]
    : []),
])

async function onPause(value: PauseRequestValue) {
  if (value.forAll) {
    const result = await lifecycle.pauseAll(value.reason, value.days, value.startsOn)
    if (!result) return

    pausing.value = false
    toast.show(pauseAllMessage(result, t.value))
    await refresh()
    return
  }

  if (!await lifecycle.pause(id.value, value.reason, value.days, value.startsOn)) return

  pausing.value = false
  await refresh()
}

async function onEndPause() {
  if (await lifecycle.endPause(id.value)) await refresh()
}

async function onVeto() {
  if (await lifecycle.toggleVeto(id.value)) await refresh()
}

async function onReminder(reminderAt: string | null) {
  if (!await lifecycle.setReminder(id.value, reminderAt)) return

  reminding.value = false
  await refresh()
}

function openEdit() {
  editing.value = true
}

async function onPreviewEdit(request: EditGoalRequest) {
  await lifecycle.previewEdit(id.value, request)
}

async function onEdit(request: EditGoalRequest) {
  if (!await lifecycle.edit(id.value, request)) return

  editing.value = false
  await refresh()
}

async function onWithdrawChange() {
  if (await lifecycle.withdrawChange(id.value)) await refresh()
}

async function onChangeVeto() {
  if (await lifecycle.toggleChangeVeto(id.value)) await refresh()
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
    >
      <!-- Where this goal is checked: its photographs, the votes on them and
           what its friends say. Grey — it goes somewhere, it does nothing. -->
      <template #actions>
        <UButton
          v-if="detail?.conversationId"
          :to="`/chats/${detail.conversationId}`"
          icon="i-lucide-message-circle"
          size="lg"
          color="neutral"
          variant="outline"
          class="min-h-11 rounded-full font-extrabold"
          data-testid="goal-open-chat"
        >
          {{ t.goals.openChat }}
        </UButton>

        <UDropdownMenu
          v-if="exits.length > 0"
          :items="exits"
          :content="{ align: 'end' }"
          :disabled="lifecycle.isBusy.value"
        >
          <UButton
            icon="i-lucide-ellipsis"
            color="neutral"
            variant="outline"
            size="lg"
            :ui="{ base: 'size-11 justify-center rounded-full' }"
            :aria-label="t.goals.moreActions"
            data-testid="goal-exits"
          />
        </UDropdownMenu>
      </template>
    </AppScreenHeader>

    <AppContentPanel>
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
              {{ goalScheduleLabel(goal, t) }}
            </p>
          </div>
        </header>

        <!--
          Whose it is, on a friend's goal (#79). The team below lists who
          checks it — for a friend's goal that is you — so without this line
          the one person it is about appeared nowhere on the screen.
        -->
        <NuxtLink
          v-if="!goal.isMine"
          :to="`/people/${goal.owner.id}`"
          class="mt-2 flex min-h-11 w-fit max-w-full items-center gap-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
          data-testid="goal-owner"
        >
          <AppAvatar
            :initials="goal.owner.initials"
            :color="goal.owner.avatarColor"
            :image-id="goal.owner.avatarImageId"
            :size="24"
          />
          <span
            class="min-w-0 truncate text-[13px] font-semibold text-(--ui-text-muted)"
            data-q2-private
          >
            {{ t.goals.owner }} <span class="font-bold text-(--ui-text)">{{ goal.owner.displayName }}</span>
          </span>
        </NuxtLink>

        <p
          v-if="goal.description"
          class="mt-3 text-sm text-(--ui-text-muted)"
          data-q2-private
          data-testid="goal-description"
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

        <GoalPauseBanner
          v-else-if="plannedPause"
          class="mt-4"
          :pause="plannedPause"
          planned
          :is-mine="goal.isMine"
          :busy="lifecycle.isBusy.value"
          @end="onEndPause"
        />

        <!-- An edit waiting for the next window: what, from when, and the
             objection to it while that is open (docs/adr/0044). -->
        <GoalChangeBanner
          v-if="pendingChange"
          class="mt-4"
          :change="pendingChange"
          :is-mine="goal.isMine"
          :busy="lifecycle.isBusy.value"
          @withdraw="onWithdrawChange"
          @veto="onChangeVeto"
        />

        <!-- Gone entirely while the goal is set aside: there is no window, so
             a ring would have nothing to show and the status word underneath
             would read "Aktiv" over a goal that is resting. The banner above
             says what is true instead. -->
        <SpotCheckCard
          v-if="goal.spotCheck"
          class="mt-4"
          :goal="goal"
          :now="now"
          :busy="isDelivering"
          @answer="(_, checkId) => answering = checkId"
          @expired="refresh()"
        />

        <DontHero
          v-if="!pause && goal.kind === 'Dont'"
          :goal="goal"
          :today="today"
          :delivering="isDelivering"
          :logging="dontUse.isLogging.value"
          @deliver="capturing = true"
          @log="onLog"
          @log-with-photo="capturingUse = true"
        />

        <section
          v-else-if="!pause"
          class="q2-card mt-4 p-5 text-center"
          aria-labelledby="goal-window-heading"
        >
          <h2
            id="goal-window-heading"
            class="sr-only"
          >
            {{ currentWindow ? windowLabel(currentWindow, t, goal.dueBy) : t.status[goal.status] }}
          </h2>

          <!-- The ring is the open window, not the goal. A goal has no
               percentage any more: it has a period, and a period either was
               delivered or was not. -->
          <AppProgressRing
            v-if="currentWindow"
            class="mx-auto"
            :percent="windowPercent(currentWindow)"
            :size="130"
            :label="windowLabel(currentWindow, t, goal.dueBy)"
          >
            <span
              class="text-[34px] leading-none font-extrabold"
              data-q2-private
            >{{ currentWindow.confirmedProofs }}/{{ currentWindow.requiredProofs }}</span>
            <span class="mt-0.5 text-[11px] font-bold text-(--ui-text-muted)">{{ windowLabel(currentWindow, t, goal.dueBy) }}</span>
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

            Only on your own goal: `acceptsProof` is about the window, not the
            reader, and only the owner hands a photograph in (#79). On a
            friend's goal the same window says whose photograph is still owed.
          -->
          <UButton
            v-if="goal.isMine && currentWindow?.acceptsProof"
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
            v-else-if="currentWindow?.acceptsProof"
            class="mt-4 text-center text-[13px] font-semibold text-(--ui-text-muted)"
            data-testid="goal-proof-owed"
            data-q2-private
          >
            {{ t.goals.proofOwedBy(goal.owner.displayName) }}
          </p>

          <p
            v-else
            class="mt-4 text-center text-[13px] font-semibold text-(--ui-text-dimmed)"
            data-testid="goal-proof-done"
          >
            {{ t.goals.recordedProof }}
          </p>
        </section>

        <!--
          Who checks it, straight after what is due: being seen by these people
          is the point of the goal, so they are not left under the history.
          Each goes to their profile.
        -->
        <section
          v-if="detail.team.length > 0"
          class="mt-3.5"
          aria-labelledby="goal-team-heading"
        >
          <h2
            id="goal-team-heading"
            class="q2-eyebrow mb-2 px-0.5"
          >
            {{ goal.isMine ? t.goals.teamMine : t.goals.teamTheirs }}
          </h2>

          <ul class="flex list-none flex-col gap-2 p-0">
            <li
              v-for="member in detail.team"
              :key="member.person.id"
              data-testid="goal-team-member"
            >
              <NuxtLink
                :to="`/people/${member.person.id}`"
                class="q2-card q2-press flex items-center gap-3 px-3 py-2.5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
              >
                <AppAvatar
                  :initials="member.person.initials"
                  :color="member.person.avatarColor"
                  :image-id="member.person.avatarImageId"
                  :size="36"
                  :online="member.person.isOnline"
                />

                <span
                  class="min-w-0 flex-1"
                  data-q2-private
                >
                  <span class="block truncate text-sm font-bold">{{ member.person.displayName }}</span>
                  <span class="block text-[11px] font-semibold text-(--ui-text-muted)">{{ t.friends.streak(member.streak) }}</span>
                </span>

                <UIcon
                  name="i-lucide-chevron-right"
                  class="size-4 shrink-0 text-(--ui-text-dimmed)"
                  aria-hidden="true"
                />
              </NuxtLink>
            </li>
          </ul>
        </section>

        <div class="mt-3.5 flex gap-2.5">
          <!-- A Do's streak. A Don't has its run at the top instead, and the
               streak — which holds through a relapse — beside it read as a
               second counter that disagreed with the first (#89, ADR 0037). -->
          <div
            v-if="goal.kind !== 'Dont'"
            class="q2-card flex-1 p-3.5"
            data-testid="goal-streak"
          >
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
            <!-- A relapse is its own number, never added to the misses. -->
            <p
              v-if="goal.windowsRelapsed > 0"
              class="text-[11px] font-semibold text-(--ui-text-dimmed)"
              data-testid="goal-relapses"
            >
              {{ t.balance.relapsed(goal.windowsRelapsed) }}
            </p>
          </div>
        </div>

        <!-- A row you can change, and it looks like one: the pencil and the
             whole card as the target. For anybody else it is only a line. -->
        <button
          v-if="canEditReminder"
          type="button"
          class="q2-card q2-press mt-2.5 flex w-full items-center gap-3 p-3.5 text-start focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
          data-testid="goal-reminder"
          @click="reminding = true"
        >
          <UIcon
            name="i-lucide-bell"
            class="size-5 shrink-0 text-(--ui-text-muted)"
            aria-hidden="true"
          />
          <span class="min-w-0 flex-1">
            <span class="block text-xs font-extrabold text-(--ui-text-muted)">{{ t.goals.reminder }}</span>
            <span
              class="block text-base font-extrabold"
              data-q2-private
            >{{ reminder ?? t.goals.noReminder }}</span>
          </span>
          <span class="sr-only">{{ t.goals.reminderEdit }}</span>
          <UIcon
            name="i-lucide-pencil"
            class="size-4 shrink-0 text-(--ui-text-muted)"
            aria-hidden="true"
          />
        </button>

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
      </article>
    </AppContentPanel>

    <PhotoCapture
      v-model:open="capturing"
      purpose="Proof"
      proof-modes
      labels-source
      :max-edge="maxEdge"
      :hand-in="handIn"
    />

    <PhotoCapture
      v-if="goal?.kind === 'Dont'"
      :open="answering !== null"
      purpose="Proof"
      proof-modes
      live-only
      :max-edge="maxEdge"
      :hand-in="handInCheck"
      @update:open="value => { if (!value) answering = null }"
    />

    <!-- Uploaded like a proof, which is what lets the goal's friends see it in
         the conversation; nobody votes on it. -->
    <PhotoCapture
      v-if="goal?.kind === 'Dont'"
      v-model:open="capturingUse"
      purpose="Proof"
      :max-edge="maxEdge"
      :hand-in="handInUse"
    />

    <GoalPauseSheet
      v-model:open="pausing"
      :remaining="goal?.remainingPauses ?? 0"
      :max-days="pauseLimits.maxDays"
      :min-reason="pauseLimits.minReason"
      :submitting="lifecycle.isBusy.value"
      :failure="lifecycle.error.value"
      :one-off="goal?.schedule.kind === 'Once'"
      :max-days-ahead="pauseLimits.maxDaysAhead"
      can-pause-all
      @submit="onPause"
    />

    <GoalReminderSheet
      v-model:open="reminding"
      :current="goal?.reminderAt ?? null"
      :submitting="lifecycle.isBusy.value"
      :failure="lifecycle.error.value"
      @save="onReminder"
    />

    <GoalCloseSheet
      v-model:open="closing"
      :submitting="lifecycle.isBusy.value"
      @close="onClose"
    />

    <GoalEditSheet
      v-if="goal && canEdit"
      v-model:open="editing"
      :goal="goal"
      :friends="friends"
      :preview="lifecycle.preview.value"
      :previewing="lifecycle.isPreviewing.value"
      :submitting="lifecycle.isBusy.value"
      :error="lifecycle.error.value"
      @preview="onPreviewEdit"
      @submit="onEdit"
    />
  </div>
</template>
