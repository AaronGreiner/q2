<script setup lang="ts">
import type { Goal } from '~/api/types'

/**
 * The top of a Don't's own screen: how long it has held, what this period
 * allows and has used, how its friends check it, and — for its owner — the tap
 * that logs a use.
 *
 * Where a Do has a ring that fills, a Don't has a run that grows: "Seit 12
 * Tagen" without a relapse. A ring would be the wrong picture. Nothing about a
 * Don't fills up during the day; it holds or it breaks
 * (docs/adr/0037-donts.md).
 *
 * The camera appears only while the period still wants its photograph (a
 * proof at the end), and it is the only accent here. Logging a use is
 * neutral: it is something to be honest about, not something to do.
 */
const props = withDefaults(defineProps<{
  goal: Goal
  /** Today as the reader's local day, for the run. */
  today: string
  delivering?: boolean
  logging?: boolean
}>(), {
  delivering: false,
  logging: false,
})

const emit = defineEmits<{ deliver: [], log: [], logWithPhoto: [] }>()

const t = useMessages()

const window = computed(() => props.goal.current)
const isRelapsed = computed(() => window.value?.status === 'Relapse')

const run = computed(() => (props.goal.runStartedOn ? runDays(props.goal.runStartedOn, props.today) : 0))
</script>

<template>
  <section
    class="q2-card mt-4 p-5 text-center"
    aria-labelledby="dont-run-heading"
    data-testid="dont-hero"
  >
    <h2
      id="dont-run-heading"
      class="q2-eyebrow"
    >
      {{ t.dont.runLabel }}
    </h2>

    <p
      class="mt-1 text-[34px] leading-tight font-extrabold"
      data-testid="dont-run"
      data-q2-private
    >
      {{ t.dont.run(run) }}
    </p>

    <p
      v-if="window"
      class="mt-2 inline-flex items-center gap-1.5 rounded-full bg-(--ui-bg-accented) px-3 py-1 text-[13px] font-bold text-(--ui-text-toned)"
      data-testid="dont-period"
      data-q2-private
    >
      <UIcon
        :name="isRelapsed ? 'i-lucide-rotate-ccw' : 'i-lucide-shield-check'"
        class="size-4"
        aria-hidden="true"
      />
      {{ windowLabel(window, t) }}
    </p>

    <p
      v-if="goal.proofMode"
      class="mt-3 text-[12px] font-semibold text-(--ui-text-muted)"
      data-testid="dont-proof-mode"
    >
      {{ t.dont.proofMode[goal.proofMode] }} · {{ t.dont.proofModeHint[goal.proofMode] }}
    </p>

    <template v-if="goal.isMine && window">
      <UButton
        v-if="window.acceptsProof"
        class="mt-4 w-full justify-center"
        size="xl"
        icon="i-lucide-camera"
        :loading="delivering"
        data-testid="goal-deliver-proof"
        @click="emit('deliver')"
      >
        {{ t.proof.deliver }}
      </UButton>

      <p
        v-else-if="window.pendingProofId"
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

      <div class="mt-3 grid grid-cols-2 gap-2">
        <DontLogButton
          :window="window"
          :title="goal.title"
          :busy="logging"
          @log="emit('log')"
        />

        <UButton
          color="neutral"
          variant="ghost"
          size="lg"
          icon="i-lucide-camera"
          :label="t.dont.logWithPhoto"
          :aria-label="t.dont.logWithPhotoFor(goal.title)"
          :disabled="logging"
          class="min-h-11 justify-center font-bold"
          data-testid="dont-log-photo"
          @click="emit('logWithPhoto')"
        />
      </div>
    </template>
  </section>
</template>
