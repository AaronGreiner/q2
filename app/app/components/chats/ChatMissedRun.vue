<script setup lang="ts">
/**
 * Several missed windows in a row, as one line: "7.–23.9. · 15 Fenster verpasst".
 *
 * The same quiet shape as `ChatEventLine`, because it is the same kind of
 * thing — what happened to the goal, not somebody speaking. Folding the run
 * is not hiding it: the count and the days are all there, once, instead of
 * fifteen identical lines between what people actually wrote.
 */
defineProps<{
  from: string
  to: string
  count: number
}>()

const t = useMessages()
</script>

<template>
  <li
    class="my-1.5 flex list-none justify-center"
    data-testid="chat-missed-run"
  >
    <p
      class="inline-flex max-w-[90%] items-center gap-1.5 rounded-full bg-(--ui-bg-elevated) px-3 py-1.5 text-center text-[12px] font-bold text-(--ui-text-muted)"
      data-q2-private
    >
      <UIcon
        name="i-lucide-circle-x"
        class="size-3.5 shrink-0"
        aria-hidden="true"
      />
      <span>{{ t.chats.missedRun(count, t.chats.dayRange(from, to)) }}</span>
    </p>
  </li>
</template>
