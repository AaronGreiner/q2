<script setup lang="ts">
/**
 * Several kept windows in a row, as one line: "22.9.–12.10. · 21 Fenster
 * geschafft · Streak 21" (#100).
 *
 * The counterpart of `ChatMissedRun`, in the same quiet shape as
 * `ChatEventLine`. The flame keeps the streak colour a kept window's own line
 * has, because it is the streak the line ends on. A Don't's run says held
 * rather than done, the way its single line does.
 */
defineProps<{
  from: string
  to: string
  count: number
  streak: number
  /** A Don't's windows: held, not done. */
  held: boolean
}>()

const t = useMessages()
</script>

<template>
  <li
    class="my-1.5 flex list-none justify-center"
    data-testid="chat-kept-run"
  >
    <p
      class="inline-flex max-w-[90%] items-center gap-1.5 rounded-full bg-(--ui-bg-elevated) px-3 py-1.5 text-center text-[12px] font-bold text-(--ui-text-muted)"
      data-q2-private
    >
      <UIcon
        name="i-lucide-flame"
        class="size-3.5 shrink-0 text-(--q2-flame-text)"
        aria-hidden="true"
      />
      <span>{{ (held ? t.chats.heldRun : t.chats.keptRun)(count, t.chats.dayRange(from, to), streak) }}</span>
    </p>
  </li>
</template>
