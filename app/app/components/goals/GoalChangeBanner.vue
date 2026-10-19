<script setup lang="ts">
import type { GoalChange } from '~/api/types'

/**
 * An edit waiting for the next window, on the goal's own screen: what it
 * changes, from when, and — for one that makes the goal easier — the objection
 * to it (docs/adr/0044-editing-a-running-goal.md).
 *
 * Grey like the pause banner: nothing has gone wrong, a goal is being changed
 * in front of the people who check it. The owner can take it back; a checker
 * can object while it is open to objection, and sees a **count without
 * names** — the API never sends who objected.
 */
const props = defineProps<{
  change: GoalChange
  /** True when the reader owns the goal: theirs to take back, not to object to. */
  isMine: boolean
  busy?: boolean
}>()

const emit = defineEmits<{ veto: [], withdraw: [] }>()

const t = useMessages()
const zone = useTimeZoneOffset()

const items = computed(() => goalChangeItems(props.change, t.value).filter(item => !item.now))
const until = computed(() => objectionDeadline(props.change, t.value, zone.value))
</script>

<template>
  <div
    class="flex flex-col gap-3 rounded-(--q2-radius-lg) border border-(--ui-border-accented) bg-(--ui-bg-elevated) px-3.5 py-3"
    data-testid="change-banner"
    data-q2-block
  >
    <div class="flex items-start gap-3">
      <UIcon
        name="i-lucide-pencil"
        class="mt-0.5 size-5 shrink-0 text-(--ui-text-muted)"
        aria-hidden="true"
      />

      <div class="flex min-w-0 flex-1 flex-col gap-1.5">
        <p
          class="text-sm font-extrabold"
          data-testid="change-banner-title"
        >
          {{ t.edit.bannerTitle }}<template v-if="change.appliesOn">
            · {{ t.edit.bannerFrom(formatWeekdayDay(change.appliesOn, t)) }}
          </template>
        </p>

        <GoalChangeList :items="items" />

        <p
          v-if="until"
          class="text-[12px] font-semibold text-(--ui-text-dimmed)"
          data-testid="change-banner-until"
        >
          {{ t.edit.objectionUntil(until) }}
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
        icon="i-lucide-undo-2"
        :label="t.edit.withdraw"
        :disabled="busy"
        data-testid="change-withdraw"
        @click="emit('withdraw')"
      />
    </div>

    <div
      v-else-if="change.canVeto"
      class="flex items-center justify-between gap-2"
    >
      <p class="text-[12px] font-semibold text-(--ui-text-dimmed)">
        {{ t.pause.vetoCount(change.vetoCount, change.vetoesRequired) }} · {{ t.pause.vetoAnonymous }}
      </p>

      <UButton
        class="min-h-11"
        size="sm"
        color="neutral"
        :variant="change.vetoedByMe ? 'solid' : 'outline'"
        icon="i-lucide-gavel"
        :label="change.vetoedByMe ? t.pause.vetoWithdraw : t.pause.veto"
        :disabled="busy"
        data-testid="change-veto"
        @click="emit('veto')"
      />
    </div>
  </div>
</template>
