<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'

/**
 * Asking for a pause: a reason, and how many days.
 *
 * The two costs the rule leans on are both on this screen rather than behind
 * it. The reason is a field that will not submit until it is a sentence, and
 * the allowance is a number in plain sight — "noch 1 Pause diesen Monat" is
 * what makes somebody spend it on the week they are actually ill.
 *
 * There is deliberately no wording that treats this as a confession. Somebody
 * setting a goal aside has not done anything wrong; the friction is the
 * scarcity, not the tone.
 *
 * It can start on a later day — a holiday known ahead (#99) — and, on a goal's
 * screen, be asked for every running Do and Don't at once; the Dos tab opens it
 * for all of them from the start (`allOnly`). Whether a goal could be set aside
 * on those days is the server's answer, goal by goal.
 */
/** What the sheet hands back: one pause, or the same pause for every running goal. */
export interface PauseRequestValue {
  reason: string
  days: number
  /** The first day, `YYYY-MM-DD`, or null for today. */
  startsOn: string | null
  forAll: boolean
}

const props = defineProps<{
  /** How many pauses are left this month, as the server counted them. */
  remaining: number
  maxDays: number
  minReason: number
  submitting: boolean
  failure?: ApiFailure | null
  /**
   * A one-off has no next window to pick up with: its window comes back once
   * the pause is over, later by the paused days (#129), and the note says so.
   */
  oneOff?: boolean
  /** Offers "für alle meine Dos & Don'ts" beside this goal (#99). */
  canPauseAll?: boolean
  /** Opened for everything at once, from the Dos tab: there is no single goal. */
  allOnly?: boolean
  /** How far ahead a pause may start, in days — `PauseRules.MaxDaysAhead`. */
  maxDaysAhead?: number
}>()

const emit = defineEmits<{ submit: [value: PauseRequestValue] }>()

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()

const now = useNow()
const zone = useTimeZoneOffset()

const reason = ref('')
const days = ref(2)
const start = ref<'today' | 'tomorrow' | 'later'>('today')
const laterDay = ref('')
const forAll = ref(false)

// Kept mounted between openings, so the draft goes back to empty each time —
// otherwise last week's excuse is still in the field.
watch(open, (isOpen) => {
  if (isOpen) {
    reason.value = ''
    days.value = 2
    start.value = 'today'
    laterDay.value = ''
    forAll.value = Boolean(props.allOnly)
  }
})

const today = computed(() => localDay(now.value, zone.value))
const earliestLater = computed(() => addDays(today.value, 2))
const latest = computed(() => addDays(today.value, props.maxDaysAhead ?? 30))

const startOptions = computed(() => [
  { value: 'today' as const, label: t.value.pause.startToday },
  { value: 'tomorrow' as const, label: t.value.pause.startTomorrow },
  { value: 'later' as const, label: t.value.pause.startLater },
])

/** The first day, or null for today; null too while "später" has no day yet. */
const startsOn = computed<string | null>(() => {
  if (start.value === 'tomorrow') return addDays(today.value, 1)
  if (start.value === 'later') return laterDay.value >= earliestLater.value && laterDay.value <= latest.value ? laterDay.value : null
  return null
})

const isPlanned = computed(() => start.value !== 'today')

/**
 * The allowance shown is this month's. A pause that begins in another month,
 * or one for every goal, is the server's to count — goal by goal, in the month
 * it begins — so only a pause of this goal from this month is held back here.
 */
const startsThisMonth = computed(() => (startsOn.value ?? today.value).slice(0, 7) === today.value.slice(0, 7))
const hasAllowance = computed(() => props.remaining > 0 || forAll.value || !startsThisMonth.value)

const trimmed = computed(() => reason.value.trim())
const isReasonTooShort = computed(() => trimmed.value.length < props.minReason)
const canSubmit = computed(() =>
  !isReasonTooShort.value
  && hasAllowance.value
  && (start.value !== 'later' || startsOn.value !== null)
  && !props.submitting)

const dayOptions = computed(() =>
  Array.from({ length: props.maxDays }, (_, index) => index + 1))

function onSubmit() {
  if (!canSubmit.value) return
  emit('submit', { reason: trimmed.value, days: days.value, startsOn: startsOn.value, forAll: forAll.value })
}
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="allOnly ? t.pause.allHeading : t.pause.heading"
    :description="allOnly ? t.pause.allSubtitle : t.pause.subtitle"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <div class="flex flex-col gap-5 pb-2">
        <p
          v-if="!allOnly"
          class="text-[13px] font-bold"
          :class="hasAllowance ? 'text-(--ui-text-muted)' : 'text-(--ui-error)'"
          data-testid="pause-allowance"
        >
          {{ remaining > 0 ? t.pause.remaining(remaining) : hasAllowance ? t.pause.monthNote : t.pause.exhausted }}
        </p>

        <UFormField
          :label="t.pause.reasonLabel"
          :help="t.pause.reasonHint(minReason)"
        >
          <UTextarea
            v-model="reason"
            :rows="3"
            :maxlength="280"
            :placeholder="t.pause.reasonPlaceholder"
            :disabled="!hasAllowance"
            class="w-full"
            data-testid="pause-reason"
          />
        </UFormField>

        <!-- The same chips the create sheet uses for a quota, and drawn the
             same way: a native radio group rather than a row of buttons, so a
             screen reader hears one choice with seven options. -->
        <div class="flex flex-col gap-1.5">
          <p class="q2-eyebrow">
            {{ t.pause.daysLabel }}
          </p>

          <div
            class="flex flex-wrap gap-1.5"
            role="radiogroup"
            :aria-label="t.pause.daysLabel"
          >
            <button
              v-for="option in dayOptions"
              :key="option"
              type="button"
              role="radio"
              :aria-checked="option === days"
              :disabled="!hasAllowance"
              class="min-h-11 rounded-(--q2-radius-md) border px-3 text-[13px] font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary) disabled:opacity-50"
              :class="option === days
                ? 'border-transparent bg-(--q2-accent-solid) text-(--q2-accent-contrast)'
                : 'border-(--ui-border) bg-(--q2-surface) text-(--ui-text-muted)'"
              :data-testid="`pause-days-${option}`"
              @click="days = option"
            >
              {{ t.pause.days(option) }}
            </button>
          </div>
        </div>

        <div class="flex flex-col gap-1.5">
          <p class="q2-eyebrow">
            {{ t.pause.startLabel }}
          </p>
          <AppSegmented
            v-model="start"
            :options="startOptions"
            :label="t.pause.startLabel"
          />
          <UFormField
            v-if="start === 'later'"
            :label="t.pause.startDateLabel"
            class="mt-1.5"
          >
            <UInput
              v-model="laterDay"
              type="date"
              :min="earliestLater"
              :max="latest"
              class="w-full"
              data-testid="pause-start-date"
            />
          </UFormField>
        </div>

        <SettingsToggleRow
          v-if="canPauseAll && !allOnly"
          v-model="forAll"
          icon="i-lucide-layers"
          :label="t.pause.forAll"
          data-testid="pause-for-all"
        />

        <p class="text-[13px] font-semibold text-(--ui-text-muted)">
          {{ forAll ? t.pause.forAllHint : oneOff ? t.pause.windowNoteOnce : t.pause.windowNote }}
          <template v-if="isPlanned">
            {{ t.pause.plannedNote }}
          </template>
        </p>

        <AppErrorState
          v-if="failure"
          :error="failure"
        />

        <UButton
          block
          size="xl"
          icon="i-lucide-pause"
          :label="t.pause.submit"
          :disabled="!canSubmit"
          :loading="submitting"
          data-testid="pause-submit"
          @click="onSubmit"
        />
      </div>
    </template>
  </UDrawer>
</template>
