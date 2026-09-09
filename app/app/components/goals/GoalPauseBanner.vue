<script setup lang="ts">
import type { GoalPause } from '~/api/types'

/**
 * A goal that is currently set aside, and what can be done about it.
 *
 * Deliberately grey rather than red. Nothing has gone wrong: no deadline is
 * running, nothing is being failed, and colouring an illness like a failure is
 * exactly the tone this product is not allowed to take
 * (docs/adr/0015-qdos-design-language.md).
 *
 * The two buttons are never both here. The owner can end their own pause; the
 * people invited can object to it, and their objection is a **count without
 * names** — the component never receives who objected, because the API never
 * sends it.
 */
defineProps<{
  pause: GoalPause
  /** True when the reader owns the goal: theirs to end, not to object to. */
  isMine: boolean
  busy?: boolean
}>()

const emit = defineEmits<{ end: [], veto: [] }>()

const t = useMessages()
</script>

<template>
  <div
    class="flex flex-col gap-3 rounded-(--q2-radius-lg) border border-(--ui-border-accented) bg-(--ui-bg-elevated) px-3.5 py-3"
    data-testid="pause-banner"
    data-q2-block
  >
    <div class="flex items-start gap-3">
      <UIcon
        name="i-lucide-pause"
        class="mt-0.5 size-5 shrink-0 text-(--ui-text-muted)"
        aria-hidden="true"
      />

      <div class="min-w-0 flex-1">
        <p class="text-sm font-extrabold">
          {{ t.pause.bannerTitle }} · {{ t.pause.until(formatDay(pause.endsOn)) }}
        </p>

        <p
          class="mt-0.5 text-[13px] font-semibold text-(--ui-text-muted)"
          data-testid="pause-reason-text"
          data-q2-private
        >
          {{ pause.reason }}
        </p>
      </div>
    </div>

    <div
      v-if="isMine"
      class="flex justify-end"
    >
      <UButton
        class="min-h-11"
        size="sm"
        color="neutral"
        variant="outline"
        icon="i-lucide-play"
        :label="t.pause.end"
        :disabled="busy"
        data-testid="pause-end"
        @click="emit('end')"
      />
    </div>

    <div
      v-else-if="pause.canVeto"
      class="flex items-center justify-between gap-2"
    >
      <p class="text-[12px] font-semibold text-(--ui-text-dimmed)">
        {{ t.pause.vetoCount(pause.vetoCount, pause.vetoesRequired) }} · {{ t.pause.vetoAnonymous }}
      </p>

      <UButton
        class="min-h-11"
        size="sm"
        color="neutral"
        :variant="pause.vetoedByMe ? 'solid' : 'outline'"
        icon="i-lucide-gavel"
        :label="pause.vetoedByMe ? t.pause.vetoWithdraw : t.pause.veto"
        :disabled="busy"
        data-testid="pause-veto"
        @click="emit('veto')"
      />
    </div>
  </div>
</template>
