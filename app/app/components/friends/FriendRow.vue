<script setup lang="ts">
import type { Friend } from '~/api/types'

/**
 * A friend, with the one thing you can do about them: write to them.
 */
const props = defineProps<{
  friend: Friend
  now: number
}>()

const emit = defineEmits<{ message: [id: string] }>()

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
    class="flex items-center gap-3 px-1.5 py-2.5"
    data-testid="friend-row"
  >
    <AppAvatar
      :initials="friend.person.initials"
      :color="friend.person.avatarColor"
      :size="42"
      :online="friend.person.isOnline"
    />

    <div class="min-w-0 flex-1">
      <p class="truncate text-sm font-bold">
        {{ friend.person.displayName }}
      </p>
      <p class="mt-0.5 truncate text-[11px] font-semibold text-(--ui-text-muted)">
        {{ subtitle }}
      </p>
    </div>

    <button
      type="button"
      class="flex size-9 shrink-0 items-center justify-center rounded-[10px] bg-(--q2-accent-soft) text-(--q2-accent-soft-text) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
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
