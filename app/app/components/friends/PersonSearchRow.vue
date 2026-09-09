<script setup lang="ts">
import type { PersonSearchResult } from '~/api/types'

/**
 * A person found by search, with whatever action makes sense for where the two
 * of you stand.
 *
 * One row rather than five, because the server sends one `state` and the whole
 * point of that field is that the client has a single thing to switch on. A
 * result you are already friends with still appears — finding somebody and
 * being told "you already know them" is a useful answer.
 */
defineProps<{ result: PersonSearchResult }>()

const emit = defineEmits<{
  request: [id: string]
  withdraw: [id: string]
  accept: [id: string]
  message: [id: string]
}>()

const t = useMessages()
</script>

<template>
  <div
    class="q2-card flex items-center gap-3 px-3 py-3"
    data-testid="person-result"
  >
    <AppAvatar
      :initials="result.person.initials"
      :color="result.person.avatarColor"
      :image-id="result.person.avatarImageId"
      :size="42"
      :online="result.person.isOnline"
    />

    <!--
      The name is the way into their profile, where their record with you is.
      A row that only offered "add" would make somebody decide before they
      could see anything about the person.
    -->
    <NuxtLink
      :to="`/people/${result.person.id}`"
      class="min-w-0 flex-1 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      data-q2-private
      data-testid="person-link"
    >
      <p class="truncate text-sm font-bold">
        {{ result.person.displayName }}
      </p>
      <p class="mt-0.5 truncate text-[11px] font-semibold text-(--ui-text-muted)">
        {{ result.person.handle }}
        <template v-if="result.mutualFriends > 0">
          · {{ t.friends.mutual(result.mutualFriends) }}
        </template>
      </p>
    </NuxtLink>

    <button
      v-if="result.state === 'None'"
      type="button"
      class="min-h-11 shrink-0 rounded-(--q2-radius-md) bg-(--q2-accent-solid) px-3 text-xs font-extrabold text-(--q2-accent-contrast) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="`${t.friends.add}: ${result.person.displayName}`"
      data-testid="result-request"
      @click="emit('request', result.person.id)"
    >
      {{ t.friends.add }}
    </button>

    <button
      v-else-if="result.state === 'RequestSent'"
      type="button"
      class="min-h-11 shrink-0 rounded-(--q2-radius-md) bg-(--q2-track) px-3 text-xs font-extrabold text-(--ui-text-muted) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="`${t.friends.withdraw}: ${result.person.displayName}`"
      data-testid="result-withdraw"
      @click="emit('withdraw', result.person.id)"
    >
      {{ t.friends.requested }}
    </button>

    <button
      v-else-if="result.state === 'RequestReceived'"
      type="button"
      class="min-h-11 shrink-0 rounded-(--q2-radius-md) bg-(--q2-accent-solid) px-3 text-xs font-extrabold text-(--q2-accent-contrast) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="`${t.friends.accept}: ${result.person.displayName}`"
      data-testid="result-accept"
      @click="emit('accept', result.person.id)"
    >
      {{ t.friends.accept }}
    </button>

    <button
      v-else-if="result.state === 'Friends'"
      type="button"
      class="flex size-11 shrink-0 items-center justify-center rounded-(--q2-radius-md) bg-(--q2-accent-soft) text-(--q2-accent-soft-text) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="`${t.friends.message}: ${result.person.displayName}`"
      data-testid="result-message"
      @click="emit('message', result.person.id)"
    >
      <UIcon
        name="i-lucide-message-circle"
        class="size-[17px]"
        aria-hidden="true"
      />
    </button>

    <span
      v-else
      class="shrink-0 text-xs font-extrabold text-(--ui-text-muted)"
    >
      {{ t.friends.you }}
    </span>
  </div>
</template>
