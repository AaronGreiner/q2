<script setup lang="ts">
import type { GoalWindow } from '~/api/types'

/**
 * The one tap that logs a use of a Don't.
 *
 * One tap, except when that use would be the one over the limit: then it asks
 * first, and says what a relapse costs and what it does not. Not to talk
 * anybody out of being honest — the confirm button is right there — but a
 * relapse cannot be taken back, and a thumb that slipped should not cost a
 * week (docs/adr/0037-donts.md).
 *
 * Neutral, never the accent: logging a use is something the person can do, but
 * it is not something the app wants them to do (ADR 0015).
 */
const props = withDefaults(defineProps<{
  /** The window running now; its uses and limit decide whether to ask. */
  window: GoalWindow
  title: string
  busy?: boolean
  /** The short form for a row: the icon alone, the title in its name. */
  compact?: boolean
}>(), {
  busy: false,
  compact: false,
})

const emit = defineEmits<{ log: [] }>()

const t = useMessages()

const isConfirming = ref(false)

/** Already relapsed counts as no: one more changes nothing, so it needs no question. */
const wouldRelapse = computed(() =>
  props.window.status === 'Open' && props.window.limit !== null && props.window.limit !== undefined
  && props.window.uses + 1 > props.window.limit)

function onTap() {
  if (wouldRelapse.value) {
    isConfirming.value = true
    return
  }

  emit('log')
}
</script>

<template>
  <UButton
    color="neutral"
    variant="outline"
    :size="compact ? 'sm' : 'lg'"
    icon="i-lucide-plus"
    :label="compact ? undefined : t.dont.log"
    :aria-label="t.dont.logFor(title)"
    :disabled="busy"
    :class="compact ? 'min-h-11 min-w-11 shrink-0 justify-center' : 'min-h-11 justify-center font-bold'"
    data-testid="dont-log"
    @click="onTap"
  />

  <AppConfirmDialog
    v-model:open="isConfirming"
    :title="t.dont.relapseHeading"
    :description="t.dont.relapseBody"
    :confirm-label="t.dont.relapseConfirm"
    @confirm="emit('log')"
  />
</template>
