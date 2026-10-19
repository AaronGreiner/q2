<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'
import type { CreateGoalRequest, Friend, GoalDraft, GoalKind, GoalScheduleRequest, SaveGoalDraftRequest } from '~/api/types'
import { goalIcons } from '~/api/types'
import type { DontLimit } from '~/components/donts/DontLimitPicker.vue'
import type { DontTemplate, GoalTemplate } from '~/utils/goalTemplates'

/**
 * The sheet that slides up from the bottom to create a goal.
 *
 * It owns its own draft and nothing else: submitting emits the request and the
 * page decides what to do with it. Server-side field errors are passed back in
 * via `error`, so the rules the user reads are the ones the API enforced — no
 * second copy of them living in the browser — worded from the catalogue by
 * the code each one carries (#80).
 *
 * **Four short steps rather than one long form.** What, how often, who checks
 * it, and a summary. Every one fits on a phone without scrolling past three
 * other questions, the friend list gets a search of its own, and the last step
 * says what is about to happen — "Dein erstes Fenster endet heute um
 * Mitternacht" — before anything is committed to.
 *
 * **Somebody has to check it.** A goal is made in front of friends, and they
 * are chosen here: the goal's conversation is opened with exactly them, and
 * they are who votes on its photographs. Without a friend there is nothing to
 * choose, so the sheet offers to keep a Do waiting instead — it starts by
 * itself when the first friend arrives — and, once one is waiting, the invite
 * link (docs/adr/0027-goal-conversations.md, docs/adr/0039-first-run-onboarding.md).
 *
 * **A Do or a Don't, decided first.** "Ich will …" or "Ich will nicht mehr …"
 * at the top of the first step. A Don't swaps the rhythm for a limit and a way
 * for friends to check it, and has no daily reminder: there is nothing to be
 * reminded to do (docs/adr/0037-donts.md).
 */
const props = withDefaults(defineProps<{
  friends: Friend[]
  friendsLoading?: boolean
  submitting?: boolean
  error?: ApiFailure | null
  /** The Do already waiting for a first friend, if there is one. */
  draft?: GoalDraft | null
  draftError?: ApiFailure | null
}>(), {
  friendsLoading: false,
  submitting: false,
  error: null,
  draft: null,
  draftError: null,
})

const emit = defineEmits<{
  submit: [request: CreateGoalRequest]
  /** Keep a Do waiting, for somebody with nobody to check it yet. */
  draft: [request: SaveGoalDraftRequest]
}>()

const draftFormId = useId()
const draftValid = ref(false)

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()
const now = useNow()
const zone = useTimeZoneOffset()

const maxTitleLength = 120
/** `Goal.MaxDescriptionLength` on the server, which stays the authority. */
const maxDescriptionLength = 1000
const stepCount = 4

const step = ref(0)
const kind = ref<GoalKind>('Do')
const title = ref('')
/** What counts, in the owner's words: what the checkers judge a proof against (#95). */
const description = ref('')
const icon = ref<string>(goalIcons[0]!)
const withReminder = ref(true)
const reminderTime = ref('09:00')
const participantIds = ref<string[]>([])

/*
 * Every day, which is what most people mean by a new habit and the one the
 * server assumes when a request says nothing about it.
 */
const schedule = ref<GoalScheduleRequest>({ kind: 'Interval', everyDays: 1 })

/** A one-off's day (#94), chosen in the picker; sent only with a one-off. */
const targetDate = ref<string | null>(null)

/** A Don't's half of the draft: never, per day, on the owner's word until changed. */
const dontLimit = ref<DontLimit>({ limit: 0, period: 'Day', proofMode: 'HonestOnly' })

const isDont = computed(() => kind.value === 'Dont')

const kindOptions = computed(() => [
  { value: 'Do' as const, label: t.value.dont.kindDo },
  { value: 'Dont' as const, label: t.value.dont.kindDont },
])

function useTemplate(template: GoalTemplate) {
  title.value = t.value.create.templates[template.key]
  icon.value = template.icon
  schedule.value = { ...template.schedule }
}

function useDontTemplate(template: DontTemplate) {
  title.value = t.value.dont.templates[template.key]
  icon.value = template.icon
  dontLimit.value = { limit: template.limit, period: template.period, proofMode: template.proofMode }
}

/** The step names, with "Limit" in place of "Rhythmus" for a Don't. */
const stepNames = computed(() => t.value.create.steps.map((name, index) =>
  (index === 1 && isDont.value ? t.value.dont.limitStep : name)))

/** The schedule a Don't is sent with: its period, which is its window. */
const dontSchedule = computed<GoalScheduleRequest>(() => ({ kind: 'Limit', period: dontLimit.value.period }))

/** Who can be asked to check it: the owner's friends. */
const friendPeople = computed(() => props.friends.map(friend => friend.person))

const chosenNames = computed(() => props.friends
  .filter(friend => participantIds.value.includes(friend.person.id))
  .map(friend => friend.person.displayName)
  .join(', '))

const scheduleText = computed(() => (isDont.value
  ? t.value.dont.limit(dontLimit.value.limit, t.value.schedule.period[dontLimit.value.period])
  : schedule.value.kind === 'Once' && targetDate.value
    ? t.value.schedule.onceBy(formatDay(targetDate.value))
    : scheduleLabel({
        kind: schedule.value.kind ?? 'Interval',
        everyDays: schedule.value.everyDays ?? null,
        weekdays: schedule.value.weekdays ?? [],
        times: schedule.value.times ?? null,
        period: schedule.value.period ?? null,
      }, t.value)))

/** The sentence that says what happens the moment the goal exists. */
const firstWindow = computed(() => {
  const today = localDay(now.value, zone.value)
  const end = isDont.value
    ? firstWindowEnd(dontSchedule.value, today)
    : firstWindowEnd(schedule.value, today, targetDate.value)

  if (end === today) return t.value.create.firstWindowToday
  if (end === addDays(today, 1)) return t.value.create.firstWindowTomorrow
  return t.value.create.firstWindowOn(formatWeekdayDay(end, t.value))
})

/** What is wrong with one field, worded in the reader's language from the server's code (#80). */
function fieldError(field: string): string | undefined {
  return fieldErrorText(props.error, field, t.value)
}

/*
 * A refusal from the server goes back to the step that holds the field, so the
 * message is read next to what it is about rather than on a summary.
 */
watch(() => props.error, (failure) => {
  if (!failure) return
  if (fieldError('title') || fieldError('description') || fieldError('icon')) step.value = 0
  else if (fieldError('schedule') || fieldError('targetDate') || fieldError('reminderAt') || fieldError('limit') || fieldError('proofMode')) step.value = 1
  else if (fieldError('participantIds')) step.value = 2
})

/** Only blocks the obviously empty cases; the API remains the authority. */
const canContinue = computed(() => {
  if (step.value === 0) return title.value.trim().length > 0
  if (step.value === 1) return isDont.value || !withReminder.value || /^\d{2}:\d{2}$/.test(reminderTime.value)
  if (step.value === 2) return participantIds.value.length > 0
  return !props.submitting
})

function next() {
  if (!canContinue.value) return
  if (step.value < stepCount - 1) step.value++
  else onSubmit()
}

function back() {
  if (step.value > 0) step.value--
}

function onSubmit() {
  if (title.value.trim().length === 0 || participantIds.value.length === 0 || props.submitting) return

  // Left out when empty rather than sent blank, the way a goal without one
  // has always been created.
  const said = description.value.trim()
  const described = said ? { description: said } : {}

  emit('submit', isDont.value
    ? {
        title: title.value.trim(),
        ...described,
        kind: 'Dont',
        schedule: dontSchedule.value,
        limit: dontLimit.value.limit,
        proofMode: dontLimit.value.proofMode,
        icon: icon.value,
        reminderAt: null,
        participantIds: [...participantIds.value],
      }
    : {
        title: title.value.trim(),
        ...described,
        schedule: schedule.value,
        ...(schedule.value.kind === 'Once' ? { targetDate: targetDate.value } : {}),
        icon: icon.value,
        reminderAt: withReminder.value ? `${reminderTime.value}:00` : null,
        participantIds: [...participantIds.value],
      })
}

/** Called by the page after a successful create. */
function reset() {
  step.value = 0
  kind.value = 'Do'
  title.value = ''
  description.value = ''
  icon.value = goalIcons[0]!
  schedule.value = { kind: 'Interval', everyDays: 1 }
  targetDate.value = null
  dontLimit.value = { limit: 0, period: 'Day', proofMode: 'HonestOnly' }
  withReminder.value = true
  reminderTime.value = '09:00'
  participantIds.value = []
}

defineExpose({ reset })
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="t.create.heading"
    :description="t.create.subtitle"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <div
        v-if="friendsLoading && friends.length === 0"
        class="flex flex-col gap-3 pb-2"
        aria-busy="true"
        aria-live="polite"
      >
        <span class="sr-only">{{ t.common.loading }}</span>
        <USkeleton class="h-12 w-full rounded-(--q2-radius-md)" />
        <USkeleton class="h-32 w-full rounded-(--q2-radius-md)" />
      </div>

      <div
        v-else-if="friends.length === 0 && draft"
        class="flex flex-col gap-3 pb-2"
        data-testid="goal-create-no-friends"
      >
        <p class="text-[13px] leading-relaxed font-semibold text-(--ui-text-muted)">
          {{ t.draft.createHasDraft }}
        </p>

        <InviteCard />
      </div>

      <div
        v-else-if="friends.length === 0"
        class="flex flex-col gap-4 pb-2"
        data-testid="goal-create-draft"
      >
        <p class="text-[13px] leading-relaxed font-semibold text-(--ui-text-muted)">
          {{ t.draft.createHint }}
        </p>

        <GoalDraftForm
          v-model:valid="draftValid"
          :form-id="draftFormId"
          :error="draftError"
          @submit="value => emit('draft', { title: value.title, icon: value.icon, schedule: value.schedule, reminderAt: value.reminderAt })"
        />

        <UButton
          type="submit"
          :form="draftFormId"
          block
          size="xl"
          icon="i-lucide-hourglass"
          class="justify-center"
          :loading="submitting"
          :disabled="!draftValid || submitting"
          data-testid="goal-draft-submit"
        >
          {{ t.onboarding.firstDo.submitWaiting }}
        </UButton>
      </div>

      <form
        v-else
        class="flex flex-col gap-4 pb-2"
        novalidate
        data-testid="goal-create-form"
        :data-step="step"
        @submit.prevent="next"
      >
        <!-- Where you are: four short bars and the step's name. -->
        <div>
          <div
            class="flex gap-1.5"
            aria-hidden="true"
          >
            <span
              v-for="index in stepCount"
              :key="index"
              class="h-1 flex-1 rounded-full"
              :class="index - 1 <= step ? 'bg-(--ui-text)' : 'bg-(--ui-bg-accented)'"
            />
          </div>
          <p
            class="mt-2 text-[11px] font-bold tracking-wide text-(--ui-text-muted) uppercase"
            aria-live="polite"
            data-testid="goal-create-step"
          >
            {{ t.create.stepOf(step + 1, stepCount) }} · {{ stepNames[step] }}
          </p>
        </div>

        <!-- 1 — what -->
        <template v-if="step === 0">
          <div>
            <p class="mb-2 text-sm font-semibold">
              {{ t.dont.kindLabel }}
            </p>
            <AppSegmented
              v-model="kind"
              :options="kindOptions"
              :label="t.dont.kindLabel"
            />
          </div>

          <UFormField
            :label="t.create.titleLabel"
            name="title"
            required
            :error="fieldError('title')"
            :hint="`${title.length}/${maxTitleLength}`"
          >
            <UInput
              v-model="title"
              :placeholder="isDont ? t.dont.titlePlaceholder : t.create.titlePlaceholder"
              :maxlength="maxTitleLength"
              autocomplete="off"
              size="xl"
              class="w-full"
              data-testid="goal-title-input"
            />
          </UFormField>

          <!-- Ideas while there is no title; once there is one, their place is
               where the owner says what counts. -->
          <UFormField
            v-if="title.trim().length > 0"
            :label="t.create.descriptionLabel"
            name="description"
            :help="t.create.descriptionHelp"
            :error="fieldError('description')"
            :hint="`${description.length}/${maxDescriptionLength}`"
          >
            <UTextarea
              v-model="description"
              :placeholder="isDont ? t.dont.descriptionPlaceholder : t.create.descriptionPlaceholder"
              :maxlength="maxDescriptionLength"
              :rows="2"
              autoresize
              :maxrows="5"
              size="lg"
              class="w-full"
              data-testid="goal-description-input"
            />
          </UFormField>

          <div v-else>
            <p class="mb-2 text-sm font-semibold text-(--ui-text-muted)">
              {{ t.create.templatesLabel }}
            </p>
            <ul
              v-if="isDont"
              class="flex list-none flex-wrap gap-2 p-0"
            >
              <li
                v-for="template in dontTemplates"
                :key="template.key"
              >
                <button
                  type="button"
                  class="q2-press inline-flex min-h-10 items-center gap-1.5 rounded-full border border-(--ui-border) bg-(--q2-surface) px-3 text-[13px] font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
                  :data-testid="`dont-template-${template.key}`"
                  @click="useDontTemplate(template)"
                >
                  <UIcon
                    name="i-lucide-ban"
                    class="size-4 text-(--ui-text-muted)"
                    aria-hidden="true"
                  />
                  {{ t.dont.templates[template.key] }}
                </button>
              </li>
            </ul>

            <ul
              v-else
              class="flex list-none flex-wrap gap-2 p-0"
            >
              <li
                v-for="template in goalTemplates"
                :key="template.key"
              >
                <button
                  type="button"
                  class="q2-press inline-flex min-h-10 items-center gap-1.5 rounded-full border border-(--ui-border) bg-(--q2-surface) px-3 text-[13px] font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
                  :data-testid="`goal-template-${template.key}`"
                  @click="useTemplate(template)"
                >
                  <UIcon
                    :name="goalIconName(template.icon)"
                    class="size-4 text-(--ui-text-muted)"
                    aria-hidden="true"
                  />
                  {{ t.create.templates[template.key] }}
                </button>
              </li>
            </ul>
          </div>

          <UFormField
            :label="t.create.iconLabel"
            name="icon"
            :error="fieldError('icon')"
          >
            <GoalIconPicker
              v-model="icon"
              :label="t.create.iconLabel"
            />
          </UFormField>
        </template>

        <!-- 2 — for a Don't: how often at most, and how friends check it -->
        <UFormField
          v-else-if="step === 1 && isDont"
          name="limit"
          :error="fieldError('limit') || fieldError('proofMode') || fieldError('schedule')"
        >
          <DontLimitPicker v-model="dontLimit" />
        </UFormField>

        <!-- 2 — how often, and when to be nudged -->
        <template v-else-if="step === 1">
          <UFormField
            :label="t.create.scheduleLabel"
            name="schedule"
            :error="fieldError('schedule') || fieldError('targetDate')"
          >
            <SchedulePicker
              v-model="schedule"
              v-model:target-date="targetDate"
            />
          </UFormField>

          <div class="flex flex-col gap-3 rounded-(--q2-radius-lg) border border-(--ui-border) bg-(--q2-surface) px-4 py-3">
            <div class="flex items-center justify-between">
              <span class="flex items-center gap-2.5">
                <UIcon
                  name="i-lucide-bell"
                  class="size-5 text-(--ui-text-muted)"
                  aria-hidden="true"
                />
                <span class="text-sm font-semibold">{{ t.create.reminderLabel }}</span>
              </span>

              <AppToggle
                v-model="withReminder"
                :label="t.create.reminderLabel"
              />
            </div>

            <UFormField
              v-if="withReminder"
              :label="t.create.reminderTimeLabel"
              name="reminderAt"
              :error="fieldError('reminderAt')"
            >
              <UInput
                v-model="reminderTime"
                type="time"
                size="lg"
                class="w-full"
                data-testid="goal-reminder-input"
              />
            </UFormField>
          </div>
        </template>

        <!-- 3 — who checks it -->
        <UFormField
          v-else-if="step === 2"
          :label="t.create.friendsLabel"
          name="participantIds"
          required
          :help="participantIds.length > 0
            ? `${t.create.friendsChosen(participantIds.length)}. ${checkersMeaning(participantIds.length, t)}`
            : t.create.friendsHint"
          :error="fieldError('participantIds')"
        >
          <CheckerPicker
            v-model="participantIds"
            :people="friendPeople"
          />
        </UFormField>

        <!-- 4 — what is about to happen -->
        <section
          v-else
          class="flex flex-col gap-3"
          data-testid="goal-create-review"
        >
          <dl
            class="flex flex-col divide-y divide-(--ui-border-muted) rounded-(--q2-radius-lg) border border-(--ui-border) bg-(--q2-surface) px-4"
            data-q2-private
          >
            <div class="flex items-center gap-3 py-3">
              <UIcon
                :name="goalIconName(icon)"
                class="size-5 shrink-0 text-(--ui-text-muted)"
                aria-hidden="true"
              />
              <dt class="sr-only">
                {{ t.create.reviewTitle }}
              </dt>
              <dd class="min-w-0 flex-1 text-[15px] font-extrabold">
                {{ title.trim() }}
              </dd>
            </div>
            <div
              v-if="description.trim()"
              class="flex flex-col gap-1 py-3 text-sm"
              data-testid="goal-review-description"
            >
              <dt class="font-semibold text-(--ui-text-muted)">
                {{ t.create.reviewDescription }}
              </dt>
              <dd class="font-bold whitespace-pre-line">
                {{ description.trim() }}
              </dd>
            </div>
            <div class="flex justify-between gap-3 py-3 text-sm">
              <dt class="font-semibold text-(--ui-text-muted)">
                {{ t.create.reviewSchedule }}
              </dt>
              <dd class="text-end font-bold">
                {{ scheduleText }}
              </dd>
            </div>
            <div class="flex justify-between gap-3 py-3 text-sm">
              <dt class="font-semibold text-(--ui-text-muted)">
                {{ t.create.reviewFriends }}
              </dt>
              <dd class="min-w-0 text-end font-bold">
                {{ chosenNames }}
              </dd>
            </div>
            <div
              v-if="isDont"
              class="flex justify-between gap-3 py-3 text-sm"
            >
              <dt class="font-semibold text-(--ui-text-muted)">
                {{ t.dont.proofModeLabel }}
              </dt>
              <dd class="text-end font-bold">
                {{ t.dont.proofMode[dontLimit.proofMode] }}
              </dd>
            </div>
            <div
              v-else
              class="flex justify-between gap-3 py-3 text-sm"
            >
              <dt class="font-semibold text-(--ui-text-muted)">
                {{ t.create.reviewReminder }}
              </dt>
              <dd class="text-end font-bold">
                {{ withReminder ? reminderTime : t.create.reviewNoReminder }}
              </dd>
            </div>
          </dl>

          <p
            class="flex items-start gap-2 text-[13px] leading-relaxed font-semibold text-(--ui-text-muted)"
            data-testid="goal-first-window"
          >
            <UIcon
              name="i-lucide-hourglass"
              class="mt-0.5 size-4 shrink-0"
              aria-hidden="true"
            />
            {{ firstWindow }}
          </p>
        </section>

        <!--
          A non-field error (offline, 5xx) belongs above the button where the
          user is looking after pressing it.
        -->
        <p
          v-if="error && Object.keys(error.fieldErrors).length === 0"
          class="text-sm text-(--ui-error)"
          role="alert"
          data-testid="goal-create-error"
        >
          {{ t.errors[error.kind] }}
        </p>

        <div class="flex gap-2.5">
          <UButton
            v-if="step > 0"
            type="button"
            size="xl"
            color="neutral"
            variant="outline"
            icon="i-lucide-chevron-left"
            class="justify-center"
            :disabled="submitting"
            data-testid="goal-back"
            @click="back"
          >
            {{ t.common.back }}
          </UButton>

          <UButton
            type="submit"
            :icon="step === stepCount - 1 ? 'i-lucide-sparkles' : undefined"
            :trailing-icon="step === stepCount - 1 ? undefined : 'i-lucide-chevron-right'"
            size="xl"
            class="flex-1 justify-center"
            :loading="step === stepCount - 1 && submitting"
            :disabled="!canContinue"
            :data-testid="step === stepCount - 1 ? 'goal-submit' : 'goal-next'"
          >
            {{ step === stepCount - 1 ? t.create.submit : t.create.next }}
          </UButton>
        </div>
      </form>
    </template>
  </UDrawer>
</template>
