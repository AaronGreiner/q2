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

/**
 * What comes next on your own goal, when the row would otherwise only repeat
 * something that happened to it: "Heute fällig · noch 9 Std.". Null when there
 * is nothing to deliver right now, or when somebody wrote something newer.
 */
const nextStep = computed(() => {
  const window = props.chat.goalCurrent
  const quiet = !props.chat.lastMessage && !props.chat.lastMessageHasPhoto && !props.chat.lastMessageIsDeleted
  if (!window?.acceptsProof || !quiet) return null

  const left = deadlineLeft(window.dueAt, props.now, t.value)
  return left ? t.value.chats.nextStep(windowLabel(window, t.value), left) : windowLabel(window, t.value)
})

/** "Du: ", "Lena: ", or nothing at all in a direct chat. */
const preview = computed(() => {
  if (props.chat.awaitingMyVote) return t.value.chats.awaitingVote
  if (nextStep.value) return nextStep.value
  if (props.chat.lastEvent) return t.value.chats.lastEvent[props.chat.lastEvent]

  // Taken back (#25): said, rather than an empty line or "Noch nichts".
  if (props.chat.lastMessageIsDeleted) return t.value.chats.deleted
  if (!props.chat.lastMessage && !props.chat.lastMessageHasPhoto) return t.value.chats.empty

  const prefix = props.chat.lastMessageIsMine
    ? `${t.value.chats.you}: `
    : props.chat.lastMessageSenderName
      ? `${props.chat.lastMessageSenderName}: `
      : ''

  // Words win when there are any; a photograph alone is named as one.
  return `${prefix}${props.chat.lastMessage ?? t.value.chats.photo}`
})
</script>

<template>
  <NuxtLink
    :to="`/chats/${chat.id}`"
    class="q2-press flex items-center gap-3 border-b border-(--ui-border-muted) py-[13px] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    data-testid="chat-row"
  >
    <AppAvatar
      :initials="chat.initials"
      :color="chat.avatarColor"
      :image-id="chat.avatarImageId"
      :icon="chat.icon"
      :size="48"
      :online="chat.isOnline"
    />

    <div
      class="min-w-0 flex-1"
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
          <!-- Pinned to the top of its section of the list (#24). -->
          <template v-if="chat.isPinned">
            <UIcon
              name="i-lucide-pin"
              class="size-3.5 shrink-0 text-(--ui-text-dimmed)"
              aria-hidden="true"
              data-testid="chat-pinned"
            />
            <span class="sr-only">{{ t.chats.pinned }}</span>
          </template>

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
