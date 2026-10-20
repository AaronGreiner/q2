<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'
import type { EditGoalRequest, Friend, Goal, GoalCategory, GoalEditPreview, GoalSchedule, GoalScheduleRequest, Person, ScheduleKind } from '~/api/types'
import type { DontLimit } from '~/components/donts/DontLimitPicker.vue'

/**
 * The sheet that changes a running Do or Don't, opened from the "…" on its
 * screen (docs/adr/0044-editing-a-running-goal.md).
 *
 * The create sheet's four steps — what, how often, who checks it, and a
 * summary — with the kind left out: a Do does not become a Don't, and a
 * one-off stays a one-off. It opens on the goal as it will be, so a change
 * already waiting is where the owner starts from rather than something an edit
 * of the title quietly takes back.
 *
 * **The summary is the server's.** Entering it asks for a preview, and the
 * sheet says what that answers: what applies now, what waits and from which
 * day, and — before an easier change is saved — that the checkers can object
 * and until when. Which change counts as easier is a rule on the server
 * (`GoalChangeRules`), and a second copy of it here would drift.
 */
const props = withDefaults(defineProps<{
  goal: Goal
  friends: Friend[]
  preview?: GoalEditPreview | null
  previewing?: boolean
  submitting?: boolean
  error?: ApiFailure | null
}>(), {
  preview: null,
  previewing: false,
  submitting: false,
  error: null,
})

const emit = defineEmits<{
  /** Asks what the edit would do, for the summary. */
  preview: [request: EditGoalRequest]
  submit: [request: EditGoalRequest]
}>()

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()
const zone = useTimeZoneOffset()

const maxTitleLength = 120
/** `Goal.MaxDescriptionLength` on the server, which stays the authority. */
const maxDescriptionLength = 1000
const stepCount = 4

const step = ref(0)
const title = ref('')
const description = ref('')
const category = ref<GoalCategory>('Other')
const schedule = ref<GoalScheduleRequest>({ kind: 'Interval', everyDays: 1 })
const targetDate = ref<string | null>(null)
const dontLimit = ref<DontLimit>({ limit: 0, period: 'Day', proofMode: 'HonestOnly' })
const participantIds = ref<string[]>([])

const isDont = computed(() => props.goal.kind === 'Dont')
const isOnce = computed(() => props.goal.schedule.kind === 'Once')

/** A one-off stays a one-off and a repeating goal keeps repeating; the server refuses the rest. */
const scheduleKinds = computed<ScheduleKind[]>(() => (isOnce.value ? ['Once'] : ['Interval', 'Weekdays', 'Times']))

const stepNames = computed(() => t.value.create.steps.map((name, index) =>
  (index === 1 ? (isDont.value ? t.value.edit.limitStep : isOnce.value ? t.value.edit.dayStep : name) : name)))

function asRequest(value: GoalSchedule): GoalScheduleRequest {
  return {
    kind: value.kind,
    everyDays: value.everyDays ?? undefined,
    weekdays: value.weekdays.length > 0 ? [...value.weekdays] : undefined,
    times: value.times ?? undefined,
    period: value.period ?? undefined,
  }
}

/** The goal as it will be once the waiting change applies: where an edit starts. */
function startFrom(goal: Goal) {
  const waiting = goal.pendingChange ?? null
  const plan = waiting?.scheduleTo ?? goal.schedule
  const laterDay = waiting?.targetDateFrom && waiting.targetDateTo && waiting.targetDateTo > waiting.targetDateFrom
    ? waiting.targetDateTo
    : null
  const removed = new Set((waiting?.checkersRemoved ?? []).map(person => person.id))

  step.value = 0
  title.value = goal.title
  description.value = (waiting?.descriptionChanged ? waiting.descriptionTo : goal.description) ?? ''
  category.value = goal.category
  schedule.value = asRequest(plan)
  targetDate.value = laterDay ?? goal.current?.dueOn ?? goal.targetDate ?? null
  dontLimit.value = {
    limit: waiting?.limitTo ?? goal.limit ?? 0,
    period: plan.period ?? 'Day',
    proofMode: waiting?.proofModeTo ?? goal.proofMode ?? 'HonestOnly',
  }
  participantIds.value = goal.participants.map(person => person.id).filter(id => !removed.has(id))
}

watch(open, (isOpen) => {
  if (isOpen) startFrom(props.goal)
}, { immediate: true })

/**
 * Who can check it: the people on it now — friends or not, they stay until
 * removed — and the owner's friends, who can be added.
 */
const people = computed<Person[]>(() => {
  const listed = new Map<string, Person>()

  for (const person of props.goal.participants) listed.set(person.id, person)
  for (const friend of props.friends) {
    if (!listed.has(friend.person.id)) listed.set(friend.person.id, friend.person)
  }

  return [...listed.values()]
})

/**
 * Everything, every time: a field the owner did not touch is sent as it
 * stands, which is what tells the server it stays.
 */
const request = computed<EditGoalRequest>(() => ({
  title: title.value.trim(),
  description: description.value.trim(),
  category: category.value,
  schedule: isDont.value ? { kind: 'Limit', period: dontLimit.value.period } : schedule.value,
  ...(isOnce.value && targetDate.value ? { targetDate: targetDate.value } : {}),
  ...(isDont.value ? { limit: dontLimit.value.limit, proofMode: dontLimit.value.proofMode } : {}),
  participantIds: [...participantIds.value],
}))

const change = computed(() => props.preview?.change ?? null)
const items = computed(() => (change.value ? goalChangeItems(change.value, t.value) : []))
const nowItems = computed(() => items.value.filter(item => item.now))
const laterItems = computed(() => items.value.filter(item => !item.now))
const until = computed(() => (change.value ? objectionDeadline(change.value, t.value, zone.value) : null))
const nothing = computed(() => Boolean(props.preview) && !change.value && !props.preview?.withdrawsPending)

/** What is wrong with one field, worded in the reader's language from the server's code. */
function fieldError(field: string): string | undefined {
  return fieldErrorText(props.error, field, t.value)
}

/* A refusal goes back to the step that holds the field, as in the create sheet. */
watch(() => props.error, (failure) => {
  if (!failure) return
  if (fieldError('title') || fieldError('description') || fieldError('category')) step.value = 0
  else if (fieldError('schedule') || fieldError('targetDate') || fieldError('limit') || fieldError('proofMode')) step.value = 1
  else if (fieldError('participantIds')) step.value = 2
})

/** Only blocks the obviously empty cases; the API remains the authority. */
const canContinue = computed(() => {
  if (step.value === 0) return title.value.trim().length > 0
  if (step.value === 1) return !isOnce.value || Boolean(targetDate.value)
  if (step.value === 2) return participantIds.value.length > 0 || props.goal.participants.length === 0
  return Boolean(props.preview) && !nothing.value && !props.previewing && !props.submitting
})

function next() {
  if (!canContinue.value) return

  if (step.value < stepCount - 1) {
    step.value++
    if (step.value === stepCount - 1) emit('preview', request.value)
  }
  else {
    emit('submit', request.value)
  }
}

function back() {
  if (step.value > 0) step.value--
}
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="t.edit.heading"
    :description="t.edit.subtitle"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <form
        class="flex flex-col gap-4 pb-2"
        novalidate
        data-testid="goal-edit-form"
        :data-step="step"
        @submit.prevent="next"
      >
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
            data-testid="goal-edit-step"
          >
            {{ t.create.stepOf(step + 1, stepCount) }} · {{ stepNames[step] }}
          </p>
        </div>

        <!-- 1 — what -->
        <template v-if="step === 0">
          <UFormField
            :label="t.create.titleLabel"
            name="title"
            required
            :error="fieldError('title')"
            :hint="`${title.length}/${maxTitleLength}`"
          >
            <UInput
              v-model="title"
              :maxlength="maxTitleLength"
              autocomplete="off"
              size="xl"
              class="w-full"
              data-testid="goal-edit-title"
            />
          </UFormField>

          <UFormField
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
              data-testid="goal-edit-description"
            />
          </UFormField>

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

        <!-- 2 — how often, or for a one-off by when -->
        <UFormField
          v-else-if="step === 1"
          :label="t.create.scheduleLabel"
          name="schedule"
          :error="fieldError('schedule') || fieldError('targetDate')"
        >
          <SchedulePicker
            v-model="schedule"
            v-model:target-date="targetDate"
            :kinds="scheduleKinds"
          />
        </UFormField>

        <!-- 3 — who checks it -->
        <UFormField
          v-else-if="step === 2"
          :label="t.create.friendsLabel"
          name="participantIds"
          required
          :help="[t.edit.checkersHint, checkersMeaning(participantIds.length, t)].filter(Boolean).join(' ')"
          :error="fieldError('participantIds')"
        >
          <CheckerPicker
            v-model="participantIds"
            :people="people"
          />
        </UFormField>

        <!-- 4 — what saving it does, as the server says -->
        <section
          v-else
          class="flex flex-col gap-3"
          data-testid="goal-edit-review"
        >
          <div
            v-if="previewing || (!preview && !error)"
            class="flex flex-col gap-3"
            aria-busy="true"
            aria-live="polite"
          >
            <span class="sr-only">{{ t.edit.checking }}</span>
            <USkeleton class="h-20 w-full rounded-(--q2-radius-lg)" />
          </div>

          <p
            v-else-if="nothing"
            class="text-[13px] font-semibold text-(--ui-text-muted)"
            role="status"
            data-testid="goal-edit-nothing"
          >
            {{ t.edit.nothing }}
          </p>

          <template v-else-if="preview">
            <div
              v-if="nowItems.length > 0"
              class="flex flex-col gap-2 rounded-(--q2-radius-lg) border border-(--ui-border) bg-(--q2-surface) px-4 py-3"
              data-testid="goal-edit-now"
            >
              <p class="text-xs font-extrabold text-(--ui-text-muted)">
                {{ t.edit.now }}
              </p>
              <GoalChangeList :items="nowItems" />
            </div>

            <div
              v-if="change && laterItems.length > 0"
              class="flex flex-col gap-2 rounded-(--q2-radius-lg) border border-(--ui-border) bg-(--q2-surface) px-4 py-3"
              data-testid="goal-edit-later"
            >
              <p class="text-xs font-extrabold text-(--ui-text-muted)">
                {{ goalChangeTiming(change, t) }}
              </p>
              <GoalChangeList :items="laterItems" />
            </div>

            <p
              v-if="change && laterItems.length > 0"
              class="flex items-start gap-2 text-[13px] leading-relaxed font-semibold text-(--ui-text-muted)"
              data-testid="goal-edit-notice"
            >
              <UIcon
                :name="until ? 'i-lucide-gavel' : 'i-lucide-hourglass'"
                class="mt-0.5 size-4 shrink-0"
                aria-hidden="true"
              />
              {{ until ? t.edit.objectable(until) : t.edit.stricter }}
            </p>

            <p
              v-if="preview.withdrawsPending || (goal.pendingChange && laterItems.length > 0)"
              class="text-[13px] font-semibold text-(--ui-text-muted)"
              data-testid="goal-edit-replaces"
            >
              {{ preview.withdrawsPending ? t.edit.withdraws : t.edit.replaces }}
            </p>
          </template>
        </section>

        <!-- A non-field error (offline, 5xx) belongs above the button. -->
        <p
          v-if="error && Object.keys(error.fieldErrors).length === 0"
          class="text-sm text-(--ui-error)"
          role="alert"
          data-testid="goal-edit-error"
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
            data-testid="goal-edit-back"
            @click="back"
          >
            {{ t.common.back }}
          </UButton>

          <UButton
            type="submit"
            :icon="step === stepCount - 1 ? 'i-lucide-check' : undefined"
            :trailing-icon="step === stepCount - 1 ? undefined : 'i-lucide-chevron-right'"
            size="xl"
            class="flex-1 justify-center"
            :loading="step === stepCount - 1 && submitting"
            :disabled="!canContinue"
            :data-testid="step === stepCount - 1 ? 'goal-edit-submit' : 'goal-edit-next'"
          >
            {{ step === stepCount - 1 ? t.edit.submit : t.create.next }}
          </UButton>
        </div>
      </form>
    </template>
  </UDrawer>
</template>
