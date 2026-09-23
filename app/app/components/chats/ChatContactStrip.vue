<script setup lang="ts">
import type { Person } from '~/api/types'

/** Existing friends are shortcuts into the same direct-chat flow as the picker. */
defineProps<{ people: Person[], busy?: boolean }>()
const emit = defineEmits<{ open: [personId: string] }>()
const t = useMessages()
</script>

<template>
  <section
    v-if="people.length"
    class="shrink-0 pb-5"
    :aria-label="t.friends.heading"
  >
    <h2 class="px-[22px] pb-3 text-xs font-semibold text-(--ui-text-muted)">
      {{ t.friends.heading }}
    </h2>
    <div class="q2-scroll-x flex gap-4 px-[22px] pt-1">
      <UButton
        v-for="person in people"
        :key="person.id"
        color="neutral"
        variant="ghost"
        :disabled="busy"
        class="w-14 shrink-0 flex-col gap-2 rounded-none p-0 text-(--ui-text-muted)"
        :aria-label="`${t.friends.message}: ${person.displayName}`"
        data-testid="chat-contact"
        data-q2-block
        @click="emit('open', person.id)"
      >
        <AppAvatar
          :initials="person.initials"
          :color="person.avatarColor"
          :image-id="person.avatarImageId"
          :size="52"
          class="rounded-full ring-1 ring-(--ui-border-accented) ring-offset-2 ring-offset-(--ui-bg)"
        />
        <span class="w-full truncate text-center text-[11px] font-semibold">{{ person.displayName }}</span>
      </UButton>
    </div>
  </section>
</template>
