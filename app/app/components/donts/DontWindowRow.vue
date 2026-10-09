<script setup lang="ts">
import type { Goal } from '~/api/types'

/**
 * One Don't on today's list: its limit, how much of it is used, and the tap
 * that logs one more.
 *
 * Its own row rather than a branch of `GoalWindowRow`, because almost nothing
 * there applies. A Do is delivered and then struck through; a Don't is kept by
 * the day running out, so it is never "done" during it, and its control logs a
 * use instead of opening a camera. Where it does take a photograph (a proof at
 * the end), the camera comes first, exactly as on a Do.
 *
 * The leading icon is a shield, not a tick box: a state, drawn in the text
 * colour, which says "holding" until the period says otherwise.
 */
const props = withDefaults(defineProps<{
  goal: Goal
  busy?: boolean
}>(), {
  busy: false,
})

const emit = defineEmits<{ deliver: [id: string], log: [id: string] }>()

const t = useMessages()

const currentWindow = computed(() => props.goal.current)
const isRelapsed = computed(() => currentWindow.value?.status === 'Relapse')
const canDeliver = computed(() => Boolean(currentWindow.value?.acceptsProof) && props.goal.isMine && !props.busy)
const isWaiting = computed(() => Boolean(currentWindow.value?.pendingProofId))
</script>

<template>
  <div
    class="q2-card flex items-center gap-3 px-3.5 py-3"
    data-testid="dont-row"
    data-q2-block
  >
    <!-- The camera only while the period still wants its photograph, and the
         accent only on it: that is the one thing to do now. -->
    <button
      v-if="canDeliver"
      type="button"
      class="q2-press relative flex size-[26px] shrink-0 items-center justify-center rounded-full bg-(--q2-accent-solid) text-(--q2-accent-contrast) after:absolute after:-inset-2.5 after:content-[''] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="`${t.proof.deliver}: ${goal.title}`"
      data-testid="window-deliver"
      @click="emit('deliver', goal.id)"
    >
      <UIcon
        name="i-lucide-camera"
        class="size-3.5"
        aria-hidden="true"
      />
    </button>

    <span
      v-else
      class="flex size-[26px] shrink-0 items-center justify-center rounded-full border-2 border-(--ui-border-accented)"
      :aria-label="isRelapsed ? t.window.relapse : isWaiting ? t.proof.waiting : t.window.kept"
      role="img"
      data-testid="dont-state"
    >
      <UIcon
        :name="isRelapsed ? 'i-lucide-rotate-ccw' : isWaiting ? 'i-lucide-hourglass' : 'i-lucide-shield-check'"
        class="size-3.5 text-(--ui-text-muted)"
        aria-hidden="true"
      />
    </span>

    <NuxtLink
      :to="`/goals/${goal.id}`"
      class="min-w-0 flex-1 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    >
      <p
        class="text-sm font-bold text-(--ui-text)"
        data-q2-private
      >
        {{ goal.title }}
      </p>

      <div class="mt-1 flex flex-wrap items-center gap-2">
        <span class="inline-flex items-center gap-1 rounded-full bg-(--ui-bg-accented) px-2 py-0.5 text-[11px] font-bold text-(--ui-text-toned)">
          <UIcon
            name="i-lucide-ban"
            class="size-3"
            aria-hidden="true"
          />
          {{ goalScheduleLabel(goal, t) }}
        </span>

        <span
          v-if="currentWindow"
          class="text-[11px] font-bold text-(--ui-text-muted)"
          data-testid="dont-used"
        >{{ windowLabel(currentWindow, t) }}</span>
      </div>
    </NuxtLink>

    <DontLogButton
      v-if="goal.isMine && currentWindow"
      :window="currentWindow"
      :title="goal.title"
      :busy="busy"
      compact
      @log="emit('log', goal.id)"
    />
  </div>
</template>
