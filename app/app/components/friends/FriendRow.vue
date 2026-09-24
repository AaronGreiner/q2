<script setup lang="ts">
import type { Friend } from '~/api/types'

/**
 * A friend, and the thing you do with them most: write to them.
 *
 * Ending the friendship is not here. It sat beside the message button, one
 * slip of the thumb from the action people press every day; it is on their
 * profile now, behind the ellipsis and a question (pages/people/[id].vue).
 */
const props = defineProps<{
  friend: Friend
  now: number
}>()

const emit = defineEmits<{
  message: [id: string]
}>()

const t = useMessages()

/**
 * Their streak if they have one, otherwise when they were last around. Presence
 * is never conveyed by the green dot alone.
 */
const subtitle = computed(() => {
  if (props.friend.streak > 0) return t.value.friends.streak(props.friend.streak)
  if (props.friend.person.isOnline) return t.value.chats.online
  if (props.friend.lastSeenAt) {
    return t.value.chats.lastSeen(formatRelativeTime(props.friend.lastSeenAt, props.now, t.value))
  }
  return t.value.chats.offline
})
</script>

<template>
  <div
    class="flex items-center gap-3 border-b border-(--ui-border-muted) py-[13px]"
    data-testid="friend-row"
  >
    <AppAvatar
      :initials="friend.person.initials"
      :color="friend.person.avatarColor"
      :image-id="friend.person.avatarImageId"
      :size="48"
      :online="friend.person.isOnline"
    />

    <!-- The way to their record with you, which is the point of a friend. -->
    <NuxtLink
      :to="`/people/${friend.person.id}`"
      class="min-w-0 flex-1 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      data-q2-private
      data-testid="friend-link"
    >
      <p class="truncate text-sm font-bold">
        {{ friend.person.displayName }}
      </p>
      <p class="mt-0.5 truncate text-[11px] font-semibold text-(--ui-text-muted)">
        {{ subtitle }}
      </p>
    </NuxtLink>

    <button
      type="button"
      class="-me-1 flex size-11 shrink-0 items-center justify-center rounded-(--q2-radius-md) bg-(--q2-accent-soft) text-(--q2-accent-soft-text) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="`${t.friends.message}: ${friend.person.displayName}`"
      data-testid="friend-message"
      @click="emit('message', friend.person.id)"
    >
      <UIcon
        name="i-lucide-message-circle"
        class="size-[17px]"
        aria-hidden="true"
      />
    </button>
  </div>
</template>
