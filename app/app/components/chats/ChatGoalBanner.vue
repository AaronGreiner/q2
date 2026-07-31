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
    class="mb-2.5 rounded-(--q2-radius-lg) border border-(--ui-border) bg-(--q2-accent-soft) px-3.5 py-3"
    aria-labelledby="pinned-goal-heading"
    data-testid="chat-goal-banner"
  >
    <h2
      id="pinned-goal-heading"
      class="flex items-center gap-2 text-[11px] font-extrabold tracking-wide text-(--q2-accent-soft-text) uppercase"
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
        class="shrink-0 text-sm font-extrabold text-(--ui-primary)"
        data-q2-private
      >{{ goal.progressPercent }}%</span>
    </div>

    <AppProgressBar
      class="mt-2"
      :percent="goal.progressPercent"
      :height="7"
      :label="t.goals.progressLabel(goal.progressPercent)"
    />

    <UButton
      class="mt-3 min-h-11 w-full justify-center font-extrabold"
      icon="i-lucide-megaphone"
      size="lg"
      color="primary"
      data-testid="chat-cheer"
      @click="emit('cheer')"
    >
      {{ t.chats.cheerButton }}
    </UButton>
  </section>
</template>
