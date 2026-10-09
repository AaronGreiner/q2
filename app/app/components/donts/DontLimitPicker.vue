<script setup lang="ts">
import type { DontProofMode, QuotaPeriod } from '~/api/types'
import { dontProofModes, limitPeriods, maxLimit } from '~/api/types'

/**
 * The second step of making a Don't: how often at most, per what, and how its
 * friends can check it.
 *
 * One shape for every Don't: "at most N per day, week or month", where 0 is
 * "never". Two forms ("never" and "at most") would be two ways to say the same
 * thing (docs/adr/0037-donts.md). The number is a stepper rather than a field,
 * because it is small and a keyboard on a phone would cover the choices below
 * it.
 */
export interface DontLimit {
  limit: number
  period: QuotaPeriod
  proofMode: DontProofMode
}

const model = defineModel<DontLimit>({ required: true })

const t = useMessages()

function step(by: number) {
  model.value = { ...model.value, limit: Math.min(maxLimit, Math.max(0, model.value.limit + by)) }
}

const periodOptions = computed(() =>
  limitPeriods.map(period => ({ value: period, label: t.value.schedule.period[period] })))

const period = computed({
  get: () => model.value.period,
  set: (value: QuotaPeriod) => { model.value = { ...model.value, period: value } },
})

const explanation = computed(() =>
  t.value.dont.explainLimit(model.value.limit, t.value.schedule.period[model.value.period]))
</script>

<template>
  <div class="flex flex-col gap-4">
    <fieldset class="flex flex-col gap-2">
      <legend class="mb-2 text-sm font-semibold">
        {{ t.dont.limitLabel }}
      </legend>

      <div class="flex items-center gap-3">
        <UButton
          color="neutral"
          variant="outline"
          size="lg"
          icon="i-lucide-minus"
          class="size-11 justify-center"
          :aria-label="`${t.dont.limitLabel} −1`"
          :disabled="model.limit === 0"
          data-testid="dont-limit-less"
          @click="step(-1)"
        />

        <output
          class="min-w-20 flex-1 text-center text-2xl font-extrabold"
          aria-live="polite"
          data-testid="dont-limit"
        >{{ model.limit === 0 ? t.dont.limitNever : `${model.limit}×` }}</output>

        <UButton
          color="neutral"
          variant="outline"
          size="lg"
          icon="i-lucide-plus"
          class="size-11 justify-center"
          :aria-label="`${t.dont.limitLabel} +1`"
          :disabled="model.limit >= maxLimit"
          data-testid="dont-limit-more"
          @click="step(1)"
        />
      </div>
    </fieldset>

    <div>
      <p class="mb-2 text-sm font-semibold">
        {{ t.dont.periodLabel }}
      </p>
      <AppSegmented
        v-model="period"
        :options="periodOptions"
        :label="t.dont.periodLabel"
      />
      <p
        class="mt-2 text-[13px] font-semibold text-(--ui-text-muted)"
        data-testid="dont-limit-explanation"
      >
        {{ explanation }}
      </p>
    </div>

    <fieldset>
      <legend class="mb-2 text-sm font-semibold">
        {{ t.dont.proofModeLabel }}
      </legend>

      <div
        class="flex flex-col gap-2"
        role="radiogroup"
        :aria-label="t.dont.proofModeLabel"
      >
        <button
          v-for="mode in dontProofModes"
          :key="mode"
          type="button"
          role="radio"
          :aria-checked="model.proofMode === mode"
          class="flex min-h-11 flex-col items-start rounded-(--q2-radius-md) border px-3.5 py-2.5 text-start focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
          :class="model.proofMode === mode
            ? 'border-(--ui-text) bg-(--ui-bg-accented)'
            : 'border-(--ui-border) bg-(--q2-surface)'"
          :data-testid="`dont-mode-${mode}`"
          @click="model = { ...model, proofMode: mode }"
        >
          <span class="text-sm font-extrabold">{{ t.dont.proofMode[mode] }}</span>
          <span class="mt-0.5 text-[12px] font-semibold text-(--ui-text-muted)">{{ t.dont.proofModeHint[mode] }}</span>
        </button>
      </div>
    </fieldset>
  </div>
</template>
