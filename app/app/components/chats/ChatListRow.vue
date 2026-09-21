<script setup lang="ts">
import type { ChatSummary } from '~/api/types'

/**
 * One conversation in the list.
 *
 * The name, avatar and presence are already resolved by the server — for a
 * group they come from the conversation, for a goal's from the goal, for a
 * direct chat from the other person — so this component does not need to know
 * that rule exists.
 *
 * A photograph waiting for the reader's verdict is said in the accent, and it
 * is the only thing in the list that is: it is something they can do now,
 * which being unread is not.
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
  if (props.chat.awaitingMyVote) return t.value.chats.awaitingVote
  if (props.chat.lastEvent) return t.value.chats.lastEvent[props.chat.lastEvent]
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
        <span class="flex min-w-0 items-center gap-1">
          <span
            class="truncate text-[15px]"
            :class="chat.unreadCount > 0 ? 'font-extrabold' : 'font-semibold'"
          >{{ chat.name }}</span>

          <!-- Muted still counts as unread: muting is about being interrupted,
               not about what is waiting when somebody looks. -->
          <template v-if="chat.isMuted">
            <UIcon
              name="i-lucide-bell-off"
              class="size-3.5 shrink-0 text-(--ui-text-dimmed)"
              aria-hidden="true"
              data-testid="chat-muted"
            />
            <span class="sr-only">{{ t.chats.muted }}</span>
          </template>
        </span>
        <span class="shrink-0 text-[11px] font-semibold text-(--ui-text-dimmed)">{{ time }}</span>
      </div>

      <div class="mt-0.5 flex items-center justify-between gap-2">
        <span
          v-if="chat.awaitingMyVote"
          class="min-w-0 truncate rounded-full bg-(--q2-accent-soft) px-2 py-0.5 text-[12px] font-extrabold text-(--q2-accent-soft-text)"
          data-testid="chat-awaiting-vote"
        >{{ preview }}</span>

        <span
          v-else
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
