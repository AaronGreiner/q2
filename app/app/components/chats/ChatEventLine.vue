<script setup lang="ts">
import type { GoalEvent } from '~/api/types'

/**
 * A line between the messages of a goal's conversation: what happened to the
 * goal — created, kept, missed, paused, ended.
 *
 * Centred and quiet, like the date a messenger puts between days, because it is
 * not somebody speaking. Grey throughout: a kept window is a state, and so is a
 * missed one. The one colour is the flame on a kept window, which is what the
 * flame means everywhere in q2 — the streak it brought the goal to. A missed
 * window is stated and nothing more; there is no red and no surface to react
 * on, so there is nothing to rub it in with.
 */
const props = defineProps<{ event: GoalEvent }>()

const t = useMessages()

const text = computed(() => goalEventText(props.event, t.value))
const icon = computed(() => goalEventIcon(props.event.kind))
</script>

<template>
  <li
    class="my-1.5 flex list-none justify-center"
    data-testid="chat-event"
    :data-kind="event.kind"
  >
    <p
      class="inline-flex max-w-[90%] items-center gap-1.5 rounded-full bg-(--ui-bg-elevated) px-3 py-1.5 text-center text-[12px] font-bold text-(--ui-text-muted)"
      data-q2-private
    >
      <UIcon
        :name="icon"
        class="size-3.5 shrink-0"
        :class="event.kind === 'WindowDone' ? 'text-(--q2-flame-text)' : ''"
        aria-hidden="true"
      />
      <span>{{ text }}</span>
    </p>
  </li>
</template>
