<script setup lang="ts">
import type { ChatSummary } from '~/api/types'

/**
 * One conversation in the list.
 *
 * The name, avatar and presence are already resolved by the server — for a
 * group they come from the conversation, for a direct chat from the other
 * person — so this component does not need to know that rule exists.
 */
const props = defineProps<{
  chat: ChatSummary
  now: number
}>()

const t = useMessages()
const zone = useTimeZoneOffset()

const time = computed(() => formatChatTime(props.chat.lastMessageAt, props.now, t.value, zone.value))

/** "Du: ", "Lena: ", or nothing at all in a direct chat. */
const preview = computed(() => {
  if (!props.chat.lastMessage) return t.value.chats.empty

  const prefix = props.chat.lastMessageIsMine
    ? `${t.value.chats.you}: `
    : props.chat.lastMessageSenderName
      ? `${props.chat.lastMessageSenderName}: `
      : ''

  return `${prefix}${props.chat.lastMessage}`
})
</script>

<template>
  <NuxtLink
    :to="`/chats/${chat.id}`"
    class="-mx-2.5 flex items-center gap-3 rounded-(--q2-radius-lg) px-2.5 py-2.5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    data-testid="chat-row"
  >
    <AppAvatar
      :initials="chat.initials"
      :color="chat.avatarColor"
      :image-id="chat.avatarImageId"
      :icon="chat.icon"
      :size="52"
      :online="chat.isOnline"
    />

    <div
      class="min-w-0 flex-1 border-b border-(--ui-border) pb-2.5"
      data-q2-private
    >
      <div class="flex items-center justify-between gap-2">
        <span
          class="truncate text-[15px]"
          :class="chat.unreadCount > 0 ? 'font-extrabold' : 'font-semibold'"
        >{{ chat.name }}</span>
        <span class="shrink-0 text-[11px] font-semibold text-(--ui-text-dimmed)">{{ time }}</span>
      </div>

      <div class="mt-0.5 flex items-center justify-between gap-2">
        <span
          class="min-w-0 flex-1 truncate text-[13px]"
          :class="chat.unreadCount > 0 ? 'font-bold text-(--ui-text)' : 'font-medium text-(--ui-text-muted)'"
        >{{ preview }}</span>

        <span
          v-if="chat.unreadCount > 0"
          class="flex h-5 min-w-5 shrink-0 items-center justify-center rounded-full bg-(--q2-accent-solid) px-1.5 text-[11px] font-extrabold text-(--q2-accent-contrast)"
          data-q2-block
        >{{ chat.unreadCount }}</span>
      </div>
    </div>
  </NuxtLink>
</template>
