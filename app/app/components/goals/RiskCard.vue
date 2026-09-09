<script setup lang="ts">
import type { Goal } from '~/api/types'

/**
 * One of your own windows that is close to being missed.
 *
 * The other half of the promise, and the half that still has a use: somebody
 * who knows their friends are about to hear that it is getting tight can still
 * act. Somebody who finds out afterwards can only regret it.
 *
 * Two things about it are restraint rather than styling:
 *
 * - **It is red, not accent.** The accent means "you can do this now" and is
 *   rationed to exactly that; red in this product means something final
 *   (docs/adr/0015-qdos-design-language.md). A window about to be missed is
 *   neither an invitation nor yet a failure, so it borrows the red *edge*
 *   without the red fill — the camera beside it keeps the accent, because
 *   pressing it is still the thing to do.
 * - **It says what is missing and nothing else.** No streak of past misses, no
 *   "schon wieder". At this point nothing has actually gone wrong.
 */
defineProps<{
  goal: Goal
  busy?: boolean
}>()

const emit = defineEmits<{ deliver: [id: string] }>()

const t = useMessages()
</script>

<template>
  <div
    class="flex items-center gap-3 rounded-(--q2-radius-lg) border border-(--ui-error)/40 bg-(--ui-error)/8 px-3.5 py-3"
    data-testid="risk-card"
    data-q2-block
  >
    <UIcon
      name="i-lucide-clock-alert"
      class="size-5 shrink-0 text-(--ui-error)"
      aria-hidden="true"
    />

    <!-- `-my-2 py-2` rather than a taller card: the text is two short lines,
         and the tap target has to be 44px even when the words are not
         (app/AGENTS.md section 8). -->
    <NuxtLink
      :to="`/goals/${goal.id}`"
      class="-my-2 min-w-0 flex-1 py-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    >
      <p
        class="truncate text-sm font-extrabold"
        data-q2-private
      >
        {{ goal.title }}
      </p>
      <p
        v-if="goal.risk"
        class="mt-0.5 text-[12px] font-semibold text-(--ui-text-muted)"
        data-testid="risk-sentence"
      >
        {{ riskSentence(goal.risk, t) }}
      </p>
    </NuxtLink>

    <UButton
      v-if="goal.current?.acceptsProof"
      class="min-h-11 min-w-11 justify-center"
      icon="i-lucide-camera"
      size="sm"
      :disabled="busy"
      :aria-label="`${t.proof.deliver}: ${goal.title}`"
      data-testid="risk-deliver"
      @click="emit('deliver', goal.id)"
    />
  </div>
</template>
