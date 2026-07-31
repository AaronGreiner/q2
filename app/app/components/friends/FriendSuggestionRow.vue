<script setup lang="ts">
import type { FriendSuggestion } from '~/api/types'

/**
 * Somebody q2 thinks you might know.
 *
 * Once asked, the button stays and turns into "Angefragt" rather than
 * disappearing: a row that vanishes on tap leaves people wondering whether it
 * worked.
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
      :disabled="suggestion.isInvited"
      class="shrink-0 rounded-[10px] px-3 py-2 text-xs font-extrabold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :class="suggestion.isInvited
        ? 'bg-(--q2-track) text-(--ui-text-muted)'
        : 'bg-(--q2-accent-solid) text-white'"
      data-testid="suggestion-request"
      @click="emit('request', suggestion.id)"
    >
      {{ suggestion.isInvited ? t.friends.requested : t.friends.add }}
    </button>
  </div>
</template>
