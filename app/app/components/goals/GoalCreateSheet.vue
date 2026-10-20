<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'
import type { CreateGoalRequest, Friend, Goal, GoalCategory, GoalDraft, GoalKind, GoalScheduleRequest, SaveGoalDraftRequest } from '~/api/types'
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
/** What kind of thing it is about, which also draws its icon (#18). */
const category = ref<GoalCategory>('Other')
const withReminder = ref(true)
const reminderTime = ref('09:00')
const participantIds = ref<string[]>([])

/*
 * The time each day ends at instead of midnight — "bis 07:00" (#98). Only for
 * a Do whose windows are single days; a quota's week and a one-off's day end
 * at midnight.
 */
const withDueBy = ref(false)
const dueByTime = ref('07:00')

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

const takesDueBy = computed(() => !isDont.value && (schedule.value.kind === 'Interval' || schedule.value.kind === 'Weekdays'))
const dueBy = computed(() => (takesDueBy.value && withDueBy.value && /^\d{2}:\d{2}$/.test(dueByTime.value) ? dueByTime.value : null))

/** A reminder at or after the day's end would never come (#98). */
const reminderTooLate = computed(() => Boolean(dueBy.value && withReminder.value && reminderTime.value >= dueBy.value!))

/*
 * Choosing a time for the day's end moves a reminder that would come after it
 * to half an hour before — the template's 09:00 against "bis 07:00" — rather
 * than leaving the person to find out from a refusal.
 */
watch(dueBy, (time) => {
  if (!time || !withReminder.value || reminderTime.value < time) return
  const [hours, minutes] = time.split(':').map(Number) as [number, number]
  const before = Math.max(0, hours * 60 + minutes - 30)
  reminderTime.value = `${String(Math.floor(before / 60)).padStart(2, '0')}:${String(before % 60).padStart(2, '0')}`
})

const kindOptions = computed(() => [
  { value: 'Do' as const, label: t.value.dont.kindDo },
  { value: 'Dont' as const, label: t.value.dont.kindDont },
])

function useTemplate(template: GoalTemplate) {
  title.value = t.value.create.templates[template.key]
  category.value = template.category
  schedule.value = { ...template.schedule }
  withDueBy.value = Boolean(template.dueBy)
  if (template.dueBy) dueByTime.value = template.dueBy
}

function useDontTemplate(template: DontTemplate) {
  title.value = t.value.dont.templates[template.key]
  category.value = template.category
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
    : [
        scheduleLabel({
          kind: schedule.value.kind ?? 'Interval',
          everyDays: schedule.value.everyDays ?? null,
          weekdays: schedule.value.weekdays ?? [],
          times: schedule.value.times ?? null,
          period: schedule.value.period ?? null,
        }, t.value),
        ...(dueBy.value ? [t.value.schedule.dueBy(dueBy.value)] : []),
      ].join(' · ')))

/** Whether the owner chose to start tomorrow (#96). A one-off has its own day. */
const startsTomorrow = ref(false)
const canStartTomorrow = computed(() => isDont.value || schedule.value.kind !== 'Once')

const startOptions = computed(() => [
  { value: 'today' as const, label: t.value.create.startToday },
  { value: 'tomorrow' as const, label: t.value.create.startTomorrow },
])

const start = computed<'today' | 'tomorrow'>({
  get: () => (startsTomorrow.value ? 'tomorrow' : 'today'),
  set: (value) => { startsTomorrow.value = value === 'tomorrow' },
})

/**
 * Today's end has already passed for a day that ends at a time (#98): the
 * server starts it tomorrow, and the summary says so instead of offering today.
 */
const dueByPassed = computed(() => {
  if (!dueBy.value) return false
  const clock = new Date(now.value + zone.value * 60_000).toISOString().slice(11, 16)
  return clock >= dueBy.value
})

/** The first day the goal counts for. */
const startDay = computed(() => {
  const today = localDay(now.value, zone.value)
  return canStartTomorrow.value && (startsTomorrow.value || dueByPassed.value) ? addDays(today, 1) : today
})

/**
 * The first window, as the server will cut it: from the day the goal starts,
 * with its share of a period that has already begun (#96).
 */
const first = computed(() => isDont.value
  ? firstWindow(dontSchedule.value, startDay.value)
  : firstWindow(schedule.value, startDay.value, targetDate.value))

/** What a full window asks, and what the first one asks — its share (#96). */
const fullAmount = computed(() => (isDont.value
  ? dontLimit.value.limit
  : schedule.value.kind === 'Times' ? (schedule.value.times ?? 1) : 1))
const firstAmount = computed(() => prorate(fullAmount.value, first.value, isDont.value ? 0 : 1))

/** The sentence that says what the first window asks, and until when. */
const firstWindowText = computed(() => {
  const today = localDay(now.value, zone.value)
  const end = first.value.end
  const words = t.value.create.firstWindowWhen
  const at = t.value.create.firstWindowAt
  const time = dueBy.value
  const when = time
    ? (end === today ? at.today(time) : end === addDays(today, 1) ? at.tomorrow(time) : at.on(formatWeekdayDay(end, t.value), time))
    : (end === today ? words.today : end === addDays(today, 1) ? words.tomorrow : words.on(formatWeekdayDay(end, t.value)))

  return isDont.value
    ? t.value.create.firstWindowLimit(firstAmount.value, when)
    : t.value.create.firstWindowProofs(firstAmount.value, when)
})

/**
 * Whether the first window asks less than a whole one, which is worth one
 * sentence of why. Not merely whether it was cut: three a week from a Tuesday
 * is still three.
 */
const isShare = computed(() => first.value.days < first.value.fullDays && firstAmount.value < fullAmount.value)

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
  if (fieldError('title') || fieldError('description') || fieldError('category')) step.value = 0
  else if (fieldError('schedule') || fieldError('targetDate') || fieldError('reminderAt') || fieldError('dueBy') || fieldError('limit') || fieldError('proofMode')) step.value = 1
  else if (fieldError('participantIds')) step.value = 2
})

/** Only blocks the obviously empty cases; the API remains the authority. */
const canContinue = computed(() => {
  if (step.value === 0) return title.value.trim().length > 0
  if (step.value === 1) {
    if (isDont.value) return true
    if (takesDueBy.value && withDueBy.value && !dueBy.value) return false
    return (!withReminder.value || /^\d{2}:\d{2}$/.test(reminderTime.value)) && !reminderTooLate.value
  }
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
        ...(startsTomorrow.value ? { startsOn: startDay.value } : {}),
        category: category.value,
        reminderAt: null,
        participantIds: [...participantIds.value],
      }
    : {
        title: title.value.trim(),
        ...described,
        schedule: schedule.value,
        ...(schedule.value.kind === 'Once' ? { targetDate: targetDate.value } : {}),
        ...(canStartTomorrow.value && (startsTomorrow.value || dueByPassed.value) ? { startsOn: startDay.value } : {}),
        ...(dueBy.value ? { dueBy: `${dueBy.value}:00` } : {}),
        category: category.value,
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
  category.value = 'Other'
  startsTomorrow.value = false
  schedule.value = { kind: 'Interval', everyDays: 1 }
  targetDate.value = null
  dontLimit.value = { limit: 0, period: 'Day', proofMode: 'HonestOnly' }
  withReminder.value = true
  reminderTime.value = '09:00'
  withDueBy.value = false
  dueByTime.value = '07:00'
  participantIds.value = []
}

/**
 * Fills the sheet from a goal that has ended, to start it again (#102): its
 * kind, title, description, category, rhythm or limit, reminder, and whoever
 * checked it who is still a friend. The old goal keeps its record; this makes
 * a new one.
 *
 * It opens on the summary, so starting again is the one tap that creates it —
 * unless something has to be chosen anew first: a one-off's day, which has
 * passed, or somebody to check it when none of its checkers is a friend now.
 */
function startFrom(goal: Goal) {
  reset()

  kind.value = goal.kind
  title.value = goal.title
  description.value = goal.description ?? ''
  category.value = goal.category

  if (goal.kind === 'Dont') {
    dontLimit.value = {
      limit: goal.limit ?? 0,
      period: goal.schedule.period ?? 'Day',
      proofMode: goal.proofMode ?? 'HonestOnly',
    }
  }
  else {
    schedule.value = {
      kind: goal.schedule.kind,
      ...(goal.schedule.everyDays ? { everyDays: goal.schedule.everyDays } : {}),
      ...(goal.schedule.weekdays.length > 0 ? { weekdays: [...goal.schedule.weekdays] } : {}),
      ...(goal.schedule.times ? { times: goal.schedule.times } : {}),
      ...(goal.schedule.period ? { period: goal.schedule.period } : {}),
    }
    withReminder.value = Boolean(goal.reminderAt)
    reminderTime.value = goal.reminderAt?.slice(0, 5) ?? '09:00'
    withDueBy.value = Boolean(goal.dueBy)
    dueByTime.value = goal.dueBy?.slice(0, 5) ?? '07:00'
  }

  const friendIds = new Set(props.friends.map(friend => friend.person.id))
  participantIds.value = goal.participants.map(person => person.id).filter(id => friendIds.has(id))

  step.value = goal.schedule.kind === 'Once' ? 1 : participantIds.value.length === 0 ? 2 : stepCount - 1
}

defineExpose({ reset, startFrom })
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
          @submit="value => emit('draft', { title: value.title, category: value.category, schedule: value.schedule, reminderAt: value.reminderAt })"
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
                    :name="categoryIconName(template.category)"
                    class="size-4 text-(--ui-text-muted)"
                    aria-hidden="true"
                  />
                  {{ t.create.templates[template.key] }}
                </button>
              </li>
            </ul>
          </div>

          <UFormField
            :label="t.create.categoryLabel"
            name="category"
            :error="fieldError('category')"
          >
            <GoalCategoryPicker
              v-model="category"
              :label="t.create.categoryLabel"
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
              :due-by="dueBy"
            />
          </UFormField>

          <!-- Only for single days: a quota's week and a one-off's day end at midnight (#98). -->
          <div
            v-if="takesDueBy"
            class="flex flex-col gap-3 rounded-(--q2-radius-lg) border border-(--ui-border) bg-(--q2-surface) px-4 py-3"
            data-testid="goal-due-by"
          >
            <div class="flex items-center justify-between">
              <span class="flex items-center gap-2.5">
                <UIcon
                  name="i-lucide-alarm-clock"
                  class="size-5 text-(--ui-text-muted)"
                  aria-hidden="true"
                />
                <span class="text-sm font-semibold">{{ t.create.dueByLabel }}</span>
              </span>

              <AppToggle
                v-model="withDueBy"
                :label="t.create.dueByLabel"
              />
            </div>

            <UFormField
              v-if="withDueBy"
              :label="t.create.dueByTimeLabel"
              :help="t.create.dueByHint"
              name="dueBy"
              :error="fieldError('dueBy')"
            >
              <UInput
                v-model="dueByTime"
                type="time"
                size="lg"
                class="w-full"
                data-testid="goal-due-by-input"
              />
            </UFormField>
          </div>

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
              :error="fieldError('reminderAt') || (reminderTooLate && dueBy ? t.create.reminderBeforeDueBy(dueBy) : undefined)"
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
                :name="categoryIconName(category)"
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
            v-if="canStartTomorrow && dueByPassed"
            class="text-sm font-semibold text-(--ui-text-muted)"
            data-testid="goal-start-after-due-by"
          >
            {{ t.create.startsTomorrowAfterDueBy(dueBy ?? '') }}
          </p>

          <div
            v-else-if="canStartTomorrow"
            class="flex flex-col gap-2"
            data-testid="goal-start"
          >
            <p class="text-sm font-semibold text-(--ui-text-muted)">
              {{ t.create.startLabel }}
            </p>
            <AppSegmented
              v-model="start"
              :options="startOptions"
              :label="t.create.startLabel"
            />
          </div>

          <p
            class="flex items-start gap-2 text-[13px] leading-relaxed font-semibold text-(--ui-text-muted)"
            data-testid="goal-first-window"
          >
            <UIcon
              name="i-lucide-hourglass"
              class="mt-0.5 size-4 shrink-0"
              aria-hidden="true"
            />
            <span>
              {{ firstWindowText }}
              <template v-if="(startsTomorrow || dueByPassed) && canStartTomorrow">
                {{ t.create.startsTomorrowHint }}
              </template>
              <template v-else-if="isShare">
                {{ t.create.firstWindowShare }}
              </template>
            </span>
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
