<script setup lang="ts">
import type { ChatPinnedGoal } from '~/api/types'

/**
 * The shared goal pinned to the top of a thread.
 *
 * This is the point of the whole chat feature: the conversation and the thing
 * it is about, in one place, with the encouragement one tap away.
 */
defineProps<{ goal: ChatPinnedGoal }>()

const emit = defineEmits<{ cheer: [] }>()

const t = useMessages()
</script>

<template>
  <section
    class="mb-2.5 rounded-(--q2-radius-lg) border border-(--ui-border) bg-(--q2-surface) px-3.5 py-3"
    aria-labelledby="pinned-goal-heading"
    data-testid="chat-goal-banner"
  >
    <h2
      id="pinned-goal-heading"
      class="q2-eyebrow flex items-center gap-2"
    >
      <UIcon
        name="i-lucide-target"
        class="size-3.5"
        aria-hidden="true"
      />
      {{ t.chats.sharedGoal }}
    </h2>

    <div class="mt-1.5 flex items-center justify-between gap-3">
      <NuxtLink
        :to="`/goals/${goal.id}`"
        class="-my-3 min-w-0 truncate py-3 text-[15px] font-extrabold text-(--ui-text) hover:underline focus-visible:underline focus-visible:outline-none"
        data-q2-private
      >
        {{ goal.title }}
      </NuxtLink>
      <span
        v-if="goal.current"
        class="shrink-0 text-sm font-extrabold"
        data-q2-private
      >{{ windowLabel(goal.current, t) }}</span>
    </div>

    <!-- Why there is no window, when there is none. Only the day: the reason
         and the objection are on the goal's own screen, which the title above
         links to — a chat is not where somebody should be asked to judge a
         friend's illness. -->
    <p
      v-if="goal.pausedUntil"
      class="mt-1 flex items-center gap-1.5 text-[12px] font-bold text-(--ui-text-muted)"
      data-testid="chat-goal-paused"
    >
      <UIcon
        name="i-lucide-pause"
        class="size-3.5"
        aria-hidden="true"
      />
      {{ t.pause.bannerTitle }} · {{ t.pause.until(formatDay(goal.pausedUntil)) }}
    </p>

    <!-- Only when the window wants more than one: a bar that is either empty
         or full says nothing the line above it has not said. -->
    <AppProgressBar
      v-if="goal.current && goal.current.requiredProofs > 1"
      class="mt-2"
      :percent="windowPercent(goal.current)"
      :height="7"
      :label="windowLabel(goal.current, t)"
    />

    <!-- An outline, not a filled accent: the one filled control on this screen
         is the send button, and two of them would make neither loud. -->
    <UButton
      class="mt-3 min-h-11 w-full justify-center font-extrabold"
      icon="i-lucide-megaphone"
      size="lg"
      color="primary"
      variant="outline"
      data-testid="chat-cheer"
      @click="emit('cheer')"
    >
      {{ t.chats.cheerButton }}
    </UButton>
  </section>
</template>
