<script setup lang="ts">
import type { SentRequest } from '~/api/types'

/**
 * Somebody you have asked and who has not answered yet.
 *
 * A section of its own rather than a greyed-out suggestion: a request that is
 * waiting on somebody else is a different thing from a person you might know,
 * and the only action it has is taking it back.
 */
defineProps<{ request: SentRequest }>()

const emit = defineEmits<{ withdraw: [id: string] }>()

const t = useMessages()
</script>

<template>
  <div
    class="q2-card flex items-center gap-3 px-3 py-3"
    data-testid="sent-request"
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
        {{ request.person.handle }}
      </p>
    </div>

    <button
      type="button"
      class="min-h-11 shrink-0 rounded-(--q2-radius-md) bg-(--q2-track) px-3 text-xs font-extrabold text-(--ui-text-muted) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="`${t.friends.withdraw}: ${request.person.displayName}`"
      data-testid="sent-request-withdraw"
      @click="emit('withdraw', request.person.id)"
    >
      {{ t.friends.withdraw }}
    </button>
  </div>
</template>
