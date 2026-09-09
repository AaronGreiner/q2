<script setup lang="ts">
import type { ChatMessage, KudosKind } from '~/api/types'
import { kudosKinds } from '~/api/types'

/**
 * One message, and the kudos on it.
 *
 * All three kinds are offered rather than one, because "well done" has more
 * than one register and the whole product is built on people telling each other
 * they saw it. They are icons, never emoji, and the one you gave is marked by a
 * filled surface rather than by a second colour — a reaction already given is a
 * state, and the accent belongs to actions.
 */
const props = defineProps<{
  message: ChatMessage
  now: number
}>()

const emit = defineEmits<{ react: [messageId: string, kind: KudosKind] }>()

const t = useMessages()

const zone = useTimeZoneOffset()

const time = computed(() => formatClock(props.message.sentAt, zone.value))

/** How many of each kind, and whether one of them is mine. */
function reactionFor(kind: KudosKind) {
  return props.message.reactions.find(reaction => reaction.kind === kind)
}
</script>

<template>
  <li
    class="flex max-w-[80%] list-none flex-col"
    :class="message.isMine ? 'items-end self-end' : 'items-start self-start'"
    data-testid="chat-bubble"
    data-q2-block
  >
    <span
      v-if="message.senderName"
      class="mb-0.5 ms-1 text-[11px] font-extrabold text-(--ui-text-muted)"
    >{{ message.senderName }}</span>

    <!--
      Your own message is a raised grey, not the accent. A thread is mostly your
      own messages, and painting all of them the accent would make the colour
      mean "mine" instead of "something you can do".
    -->
    <p
      class="px-3.5 py-2.5 text-sm leading-snug font-medium"
      :class="message.isMine
        ? 'rounded-(--q2-radius-lg) rounded-ee-[5px] bg-(--ui-bg-accented) text-(--ui-text)'
        : 'q2-card rounded-(--q2-radius-lg) rounded-es-[5px] text-(--ui-text)'"
    >
      {{ message.text }}
    </p>

    <div
      class="mt-1 flex items-center gap-1.5 px-1"
      :class="message.isMine ? 'flex-row-reverse' : ''"
    >
      <!-- Your own message shows what came back; somebody else's is also a
           control, because the reply to a message is often just a reaction. -->
      <template v-if="message.isMine">
        <span
          v-for="reaction in message.reactions"
          :key="reaction.kind"
          class="inline-flex items-center gap-1 rounded-full border border-(--ui-border) bg-(--q2-surface) px-2 py-0.5 text-[11px] font-bold"
        >
          <UIcon
            :name="kudosIconName(reaction.kind)"
            class="size-3.5"
            aria-hidden="true"
          />
          <span class="sr-only">{{ t.kudos[reaction.kind] }}</span>
          {{ reaction.count }}
        </span>
      </template>

      <!--
        Kudos nobody has given yet are a bare icon rather than an outlined pill:
        three empty pills under every message is a row of controls where there
        should be a conversation. Giving one fills it.
      -->
      <template v-else>
        <button
          v-for="kind in kudosKinds"
          :key="kind"
          type="button"
          class="relative inline-flex items-center gap-1 rounded-full py-0.5 text-[11px] font-bold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
          :class="reactionFor(kind)?.isMine
            ? 'bg-(--q2-accent-solid) px-2 text-(--q2-accent-contrast)'
            : reactionFor(kind)
              ? 'border border-(--ui-border) bg-(--q2-surface) px-2 text-(--ui-text-muted)'
              : 'px-1.5 text-(--ui-text-dimmed)'"
          :aria-pressed="reactionFor(kind)?.isMine ?? false"
          :aria-label="t.kudos[kind]"
          :data-testid="`chat-kudos-${kind}`"
          @click="emit('react', message.id, kind)"
        >
          <UIcon
            :name="kudosIconName(kind)"
            class="size-3.5"
            aria-hidden="true"
          />
          <span v-if="reactionFor(kind)">{{ reactionFor(kind)!.count }}</span>
        </button>
      </template>

      <span class="text-[10px] font-semibold text-(--ui-text-dimmed)">{{ time }}</span>
    </div>
  </li>
</template>
