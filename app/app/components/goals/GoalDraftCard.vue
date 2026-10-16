<script setup lang="ts">
import type { GoalDraft } from '~/api/types'

/**
 * A Do that waits for its owner's first friend (docs/adr/0039-first-run-onboarding.md).
 *
 * Drawn like a goal row, but quieter: it has no window, no countdown and
 * nothing to deliver, and it says so in the one line under the title. The only
 * colour on it is the link, because sending it is the one thing that makes
 * this card turn into a running goal — the thing somebody can do about it now.
 *
 * Without `actions` it is a plain statement, for the places that already offer
 * the link next to it.
 */
const props = withDefaults(defineProps<{
  draft: GoalDraft
  actions?: boolean
  busy?: boolean
}>(), {
  actions: true,
  busy: false,
})

const emit = defineEmits<{ share: [], discard: [] }>()

const t = useMessages()

const rhythm = computed(() => scheduleLabel(props.draft.schedule, t.value))

const confirming = ref(false)

function onDiscard() {
  confirming.value = false
  emit('discard')
}
</script>

<template>
  <section
    class="q2-card px-4 py-3.5"
    :aria-label="draft.title"
    data-testid="goal-draft-card"
  >
    <div class="flex items-start gap-3">
      <span
        class="flex size-10 shrink-0 items-center justify-center rounded-full bg-(--ui-bg-accented) text-(--ui-text-muted)"
        aria-hidden="true"
      >
        <UIcon
          :name="goalIconName(draft.icon)"
          class="size-5"
        />
      </span>

      <div class="min-w-0 flex-1">
        <p
          class="truncate text-[15px] font-extrabold"
          data-q2-private
          data-testid="goal-draft-title"
        >
          {{ draft.title }}
        </p>
        <p class="mt-0.5 text-[13px] font-semibold text-(--ui-text-muted)">
          {{ rhythm }}
        </p>
      </div>
    </div>

    <p
      class="mt-3 flex items-start gap-2 text-[13px] leading-relaxed font-semibold text-(--ui-text-muted)"
      data-testid="goal-draft-waiting"
    >
      <UIcon
        name="i-lucide-hourglass"
        class="mt-0.5 size-4 shrink-0"
        aria-hidden="true"
      />
      <span>
        <span class="font-bold text-(--ui-text)">{{ t.draft.waiting }}.</span>
        {{ t.draft.waitingHint }}
      </span>
    </p>

    <div
      v-if="actions"
      class="mt-3 flex items-center gap-2"
    >
      <UButton
        class="min-h-11 flex-1 justify-center"
        size="lg"
        icon="i-lucide-share-2"
        :label="t.draft.share"
        :disabled="busy"
        data-testid="goal-draft-share"
        @click="emit('share')"
      />

      <UButton
        class="min-h-11 justify-center"
        size="lg"
        color="neutral"
        variant="ghost"
        :label="t.draft.discard"
        :disabled="busy"
        data-testid="goal-draft-discard"
        @click="confirming = true"
      />
    </div>

    <AppConfirmDialog
      v-model:open="confirming"
      :title="t.draft.discardTitle"
      :description="t.draft.discardBody"
      :confirm-label="t.draft.discardConfirm"
      @confirm="onDiscard"
    />
  </section>
</template>
