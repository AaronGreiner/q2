<script setup lang="ts">
import type { GoalScheduleRequest, QuotaPeriod, ScheduleKind, Weekday } from '~/api/types'
import { intervalChoices, quotaPeriods, scheduleKinds, weekdays } from '~/api/types'

/**
 * How often a goal is due: the kind first, then whatever that kind needs.
 *
 * Two steps rather than one long form, because the four kinds want four
 * different questions and showing all of them at once is a form where three
 * quarters of the fields are noise. What is on screen at any moment is the one
 * question the chosen kind actually asks.
 *
 * The sentence under it is not decoration. "3× pro Woche" is ambiguous until
 * somebody is told when the week ends and what happens if they only manage two
 * — so the picker says it, in the same words the goal will use afterwards.
 */
const model = defineModel<GoalScheduleRequest>({ required: true })

const t = useMessages()

const kind = computed(() => model.value.kind ?? 'Interval')

/** Everything the chosen kind needs, and nothing it does not. */
function choose(next: ScheduleKind) {
  model.value = next === 'Interval'
    ? { kind: next, everyDays: model.value.everyDays ?? 1 }
    : next === 'Weekdays'
      ? { kind: next, weekdays: model.value.weekdays?.length ? model.value.weekdays : ['Monday'] }
      : next === 'Times'
        ? { kind: next, times: model.value.times ?? 3, period: model.value.period ?? 'Week' }
        : { kind: next }
}

function toggleWeekday(day: Weekday) {
  const chosen = model.value.weekdays ?? []
  const next = chosen.includes(day) ? chosen.filter(entry => entry !== day) : [...chosen, day]

  // Never empty: a schedule with no day is one the server refuses, and taking
  // the last tick away would leave the form invalid with nothing to say why.
  if (next.length === 0) return

  model.value = { ...model.value, weekdays: next }
}

function setInterval(days: number) {
  model.value = { ...model.value, everyDays: days }
}

function setTimes(times: number) {
  model.value = { ...model.value, times }
}

function setPeriod(period: QuotaPeriod) {
  model.value = { ...model.value, period }
}

/**
 * The preview, in exactly the words the goal card will use — composed from the
 * same helpers, so the two cannot drift apart.
 */
const preview = computed(() => scheduleLabel({
  kind: kind.value,
  everyDays: model.value.everyDays ?? null,
  weekdays: model.value.weekdays ?? [],
  times: model.value.times ?? null,
  period: model.value.period ?? null,
}, t.value))

const explanation = computed(() => scheduleExplanation({
  kind: kind.value,
  everyDays: model.value.everyDays ?? null,
  weekdays: model.value.weekdays ?? [],
  times: model.value.times ?? null,
  period: model.value.period ?? null,
}, t.value))

/** How many times a quota may ask for. More than seven a week is not a habit. */
const timesChoices = computed(() => (model.value.period === 'Month' ? [1, 2, 3, 4, 5, 6, 8, 10] : [1, 2, 3, 4, 5, 6, 7]))
</script>

<template>
  <div
    class="flex flex-col gap-3"
    data-testid="schedule-picker"
  >
    <div
      class="grid grid-cols-4 gap-1.5"
      role="radiogroup"
      :aria-label="t.create.scheduleLabel"
    >
      <button
        v-for="option in scheduleKinds"
        :key="option"
        type="button"
        role="radio"
        :aria-checked="kind === option"
        class="min-h-11 rounded-(--q2-radius-md) border px-1 text-[13px] font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
        :class="kind === option
          ? 'border-transparent bg-(--q2-accent-solid) text-(--q2-accent-contrast)'
          : 'border-(--ui-border) bg-(--q2-surface) text-(--ui-text-muted)'"
        :data-testid="`schedule-kind-${option}`"
        @click="choose(option)"
      >
        {{ t.schedule.kind[option] }}
      </button>
    </div>

    <!-- Every N days. -->
    <div
      v-if="kind === 'Interval'"
      class="flex flex-col gap-1.5"
    >
      <p class="q2-eyebrow">
        {{ t.create.intervalLabel }}
      </p>
      <div
        class="flex flex-wrap gap-1.5"
        role="radiogroup"
        :aria-label="t.create.intervalLabel"
      >
        <button
          v-for="days in intervalChoices"
          :key="days"
          type="button"
          role="radio"
          :aria-checked="(model.everyDays ?? 1) === days"
          class="min-h-11 rounded-(--q2-radius-md) border px-3 text-[13px] font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
          :class="(model.everyDays ?? 1) === days
            ? 'border-transparent bg-(--q2-accent-solid) text-(--q2-accent-contrast)'
            : 'border-(--ui-border) bg-(--q2-surface) text-(--ui-text-muted)'"
          :data-testid="`schedule-interval-${days}`"
          @click="setInterval(days)"
        >
          {{ t.schedule.everyNDays(days) }}
        </button>
      </div>
    </div>

    <!-- Named weekdays. -->
    <div
      v-else-if="kind === 'Weekdays'"
      class="flex flex-col gap-1.5"
    >
      <p class="q2-eyebrow">
        {{ t.create.weekdaysLabel }}
      </p>
      <div
        class="flex gap-1.5"
        :aria-label="t.create.weekdaysLabel"
        role="group"
      >
        <button
          v-for="(day, index) in weekdays"
          :key="day"
          type="button"
          role="checkbox"
          :aria-checked="(model.weekdays ?? []).includes(day)"
          :aria-label="t.schedule.weekdayShort[index]"
          class="min-h-11 flex-1 rounded-(--q2-radius-md) border text-[13px] font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
          :class="(model.weekdays ?? []).includes(day)
            ? 'border-transparent bg-(--q2-accent-solid) text-(--q2-accent-contrast)'
            : 'border-(--ui-border) bg-(--q2-surface) text-(--ui-text-muted)'"
          :data-testid="`schedule-weekday-${day}`"
          @click="toggleWeekday(day)"
        >
          {{ t.schedule.weekdayShort[index] }}
        </button>
      </div>
    </div>

    <!-- A quota: how many, per what. -->
    <div
      v-else-if="kind === 'Times'"
      class="flex flex-col gap-1.5"
    >
      <p class="q2-eyebrow">
        {{ t.create.timesLabel }}
      </p>
      <div
        class="flex flex-wrap gap-1.5"
        role="radiogroup"
        :aria-label="t.create.timesLabel"
      >
        <button
          v-for="times in timesChoices"
          :key="times"
          type="button"
          role="radio"
          :aria-checked="(model.times ?? 3) === times"
          class="min-h-11 min-w-11 rounded-(--q2-radius-md) border text-[13px] font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
          :class="(model.times ?? 3) === times
            ? 'border-transparent bg-(--q2-accent-solid) text-(--q2-accent-contrast)'
            : 'border-(--ui-border) bg-(--q2-surface) text-(--ui-text-muted)'"
          :data-testid="`schedule-times-${times}`"
          @click="setTimes(times)"
        >
          {{ times }}
        </button>
      </div>

      <p class="q2-eyebrow mt-1.5">
        {{ t.create.periodLabel }}
      </p>
      <div
        class="flex gap-1.5"
        role="radiogroup"
        :aria-label="t.create.periodLabel"
      >
        <button
          v-for="period in quotaPeriods"
          :key="period"
          type="button"
          role="radio"
          :aria-checked="(model.period ?? 'Week') === period"
          class="min-h-11 flex-1 rounded-(--q2-radius-md) border text-[13px] font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
          :class="(model.period ?? 'Week') === period
            ? 'border-transparent bg-(--q2-accent-solid) text-(--q2-accent-contrast)'
            : 'border-(--ui-border) bg-(--q2-surface) text-(--ui-text-muted)'"
          :data-testid="`schedule-period-${period}`"
          @click="setPeriod(period)"
        >
          {{ t.schedule.period[period] }}
        </button>
      </div>
    </div>

    <p
      class="q2-card px-3 py-2.5 text-[13px] leading-snug font-semibold"
      data-testid="schedule-preview"
    >
      {{ preview }}
      <span class="mt-0.5 block text-[12px] font-medium text-(--ui-text-muted)">{{ explanation }}</span>
    </p>
  </div>
</template>
