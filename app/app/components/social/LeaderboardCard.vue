<script setup lang="ts">
import type { LeaderboardEntry } from '~/api/types'

/**
 * You and your friends, ranked by kudos.
 *
 * Shows the top three plus your own row when you are not in them — a
 * leaderboard you cannot find yourself on is just a list of other people.
 * Ranks and ties come from the server so every client agrees on them.
 */
const props = defineProps<{ entries: LeaderboardEntry[] }>()

const t = useMessages()

const visible = computed(() => {
  const top = props.entries.slice(0, 3)
  const me = props.entries.find(entry => entry.isMe)

  return me && !top.includes(me) ? [...top, me] : top
})

/** Gold, silver, bronze — then the muted default. */
const medals = ['#f59e0b', '#94a3b8', '#b45309'] as const
</script>

<template>
  <ol
    class="q2-card list-none p-1.5"
    data-testid="leaderboard"
  >
    <li
      v-for="entry in visible"
      :key="entry.person.id"
      class="flex items-center gap-3 rounded-(--q2-radius-md) px-2.5 py-2.5"
      :class="entry.isMe ? 'bg-(--q2-accent-soft)' : ''"
    >
      <span
        class="w-5 text-center text-sm font-extrabold"
        :style="{ color: medals[entry.rank - 1] ?? 'var(--ui-text-muted)' }"
      >{{ entry.rank }}</span>

      <AppAvatar
        :initials="entry.person.initials"
        :color="entry.person.avatarColor"
        :size="32"
      />

      <span
        class="min-w-0 flex-1 truncate text-sm"
        :class="entry.isMe ? 'font-extrabold' : 'font-semibold'"
      >{{ entry.isMe ? t.chats.you : entry.person.displayName }}</span>

      <span class="flex items-center gap-1 text-[13px] font-extrabold text-(--ui-primary)">
        <UIcon
          name="i-lucide-sparkles"
          class="size-3.5"
          aria-hidden="true"
        />
        {{ entry.kudos }}
      </span>
    </li>
  </ol>
</template>
