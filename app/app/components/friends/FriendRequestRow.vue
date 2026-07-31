<script setup lang="ts">
import type { FriendRequest } from '~/api/types'

/**
 * Somebody waiting for an answer, with both answers.
 *
 * Accept and decline are icon buttons in the design, so both carry a real
 * label — an unlabelled tick and cross are indistinguishable to a screen
 * reader, and this is a decision about a person.
 */
defineProps<{ request: FriendRequest }>()

const emit = defineEmits<{
  accept: [id: string]
  decline: [id: string]
}>()

const t = useMessages()
</script>

<template>
  <div
    class="q2-card flex items-center gap-3 px-3 py-3"
    data-testid="friend-request"
  >
    <AppAvatar
      :initials="request.person.initials"
      :color="request.person.avatarColor"
      :size="42"
    />

    <div class="min-w-0 flex-1">
      <p class="truncate text-sm font-bold">
        {{ request.person.displayName }}
      </p>
      <p class="mt-0.5 truncate text-[11px] font-semibold text-(--ui-text-muted)">
        {{ t.friends.mutual(request.mutualFriends) }}
      </p>
    </div>

    <button
      type="button"
      class="flex size-9 shrink-0 items-center justify-center rounded-[10px] bg-(--q2-accent-solid) text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="`${t.friends.accept}: ${request.person.displayName}`"
      data-testid="request-accept"
      @click="emit('accept', request.id)"
    >
      <UIcon
        name="i-lucide-check"
        class="size-[18px]"
        aria-hidden="true"
      />
    </button>

    <button
      type="button"
      class="flex size-9 shrink-0 items-center justify-center rounded-[10px] bg-(--q2-track) text-(--ui-text-muted) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="`${t.friends.decline}: ${request.person.displayName}`"
      data-testid="request-decline"
      @click="emit('decline', request.id)"
    >
      <UIcon
        name="i-lucide-x"
        class="size-[18px]"
        aria-hidden="true"
      />
    </button>
  </div>
</template>
