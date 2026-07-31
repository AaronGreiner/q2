<script setup lang="ts">
import type { ChatMessage } from '~/api/types'
import { messageReactions } from '~/api/types'

/**
 * One message.
 *
 * Applause is the only reaction offered by a tap — the other two exist in the
 * model and are rendered when they arrive, but a self-care app does not need a
 * reaction picker to say "well done".
 */
const props = defineProps<{
  message: ChatMessage
  now: number
}>()

const emit = defineEmits<{ react: [messageId: string, emoji: string] }>()

const t = useMessages()

const zone = useTimeZoneOffset()

const clap = messageReactions[0]
const time = computed(() => formatClock(props.message.sentAt, zone.value))
</script>

<template>
  <li
    class="flex max-w-[80%] list-none flex-col"
    :class="message.isMine ? 'items-end self-end' : 'items-start self-start'"
    data-testid="chat-bubble"
  >
    <span
      v-if="message.senderName"
      class="mb-0.5 ms-1 text-[11px] font-extrabold text-(--ui-primary)"
    >{{ message.senderName }}</span>

    <p
      class="px-3.5 py-2.5 text-sm leading-snug font-medium"
      :class="message.isMine
        ? 'rounded-(--q2-radius-lg) rounded-ee-[5px] bg-(--q2-accent-solid) text-white'
        : 'q2-card rounded-(--q2-radius-lg) rounded-es-[5px] text-(--ui-text)'"
    >
      {{ message.text }}
    </p>

    <div
      class="mt-1 flex items-center gap-1.5 px-1"
      :class="message.isMine ? 'flex-row-reverse' : ''"
    >
      <span
        v-for="reaction in message.reactions"
        :key="reaction.emoji"
        class="inline-flex items-center gap-1 rounded-full border border-(--ui-border) bg-(--q2-surface) px-2 py-0.5 text-[11px] font-bold"
      >{{ reaction.emoji }} {{ reaction.count }}</span>

      <span class="text-[10px] font-semibold text-(--ui-text-dimmed)">{{ time }}</span>

      <button
        v-if="!message.isMine"
        type="button"
        class="relative flex size-6 items-center justify-center rounded-full text-sm after:absolute after:-inset-2.5 after:content-[''] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
        :aria-pressed="message.reactions.some(reaction => reaction.emoji === clap && reaction.isMine)"
        :aria-label="t.chats.clap"
        data-testid="chat-clap"
        @click="emit('react', message.id, clap)"
      >
        {{ clap }}
      </button>
    </div>
  </li>
</template>
