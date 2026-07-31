<script setup lang="ts">
import type { FriendSuggestion } from '~/api/types'

/**
 * Somebody q2 thinks you might know, because your friends do.
 *
 * Derived from the friend graph on every read rather than stored, so asking
 * moves them out of this list and into "Gesendete Anfragen" — which is where a
 * request that is waiting on somebody belongs.
 */
defineProps<{ suggestion: FriendSuggestion }>()

const emit = defineEmits<{ request: [id: string] }>()

const t = useMessages()
</script>

<template>
  <div
    class="q2-card flex items-center gap-3 px-3 py-3"
    data-testid="friend-suggestion"
  >
    <AppAvatar
      :initials="suggestion.person.initials"
      :color="suggestion.person.avatarColor"
      :size="42"
    />

    <div class="min-w-0 flex-1">
      <p class="truncate text-sm font-bold">
        {{ suggestion.person.displayName }}
      </p>
      <p class="mt-0.5 truncate text-[11px] font-semibold text-(--ui-text-muted)">
        {{ t.friends.mutual(suggestion.mutualFriends) }}
      </p>
    </div>

    <button
      type="button"
      class="min-h-11 shrink-0 rounded-(--q2-radius-md) bg-(--q2-accent-solid) px-3 text-xs font-extrabold text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="`${t.friends.add}: ${suggestion.person.displayName}`"
      data-testid="suggestion-request"
      @click="emit('request', suggestion.person.id)"
    >
      {{ t.friends.add }}
    </button>
  </div>
</template>
