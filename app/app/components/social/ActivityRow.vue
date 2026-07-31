<script setup lang="ts">
import type { Activity } from '~/api/types'

/**
 * One line of the friends' feed, with its kudos button.
 *
 * The sentence is composed here from the structured event rather than sent as
 * text — see `activitySentence` and the server's ActivityResponse for why.
 */
const props = defineProps<{
  activity: Activity
  /** Shared clock, so SSR and the browser agree on "vor 12 Min". */
  now: number
  /** Hides the kudos button — your own history is not something to cheer. */
  readonly?: boolean
}>()

const emit = defineEmits<{ kudos: [id: string] }>()

const t = useMessages()

const sentence = computed(() => activitySentence(props.activity, t.value))
const time = computed(() => formatRelativeTime(props.activity.occurredAt, props.now, t.value))
</script>

<template>
  <div
    class="q2-card flex items-center gap-3 px-3 py-3"
    data-testid="activity-row"
  >
    <AppAvatar
      :initials="activity.actor.initials"
      :color="activity.actor.avatarColor"
      :size="40"
    />

    <div
      class="min-w-0 flex-1"
      data-q2-private
    >
      <!-- The whole sentence: it names the person and quotes what they did it
           to, which is a goal or task title. -->
      <p
        class="text-[13px] leading-snug"
      >
        <b class="font-extrabold">{{ activity.actor.displayName }}</b> {{ sentence }}
      </p>
      <p class="mt-0.5 text-[11px] font-semibold text-(--ui-text-dimmed)">
        {{ time }}
      </p>
    </div>

    <button
      v-if="!readonly"
      type="button"
      class="relative flex shrink-0 items-center gap-1 rounded-full px-3 py-1.5 transition-colors after:absolute after:-inset-y-2 after:-inset-x-1 after:content-[''] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :class="activity.hasMyKudos
        ? 'bg-(--q2-accent-solid) text-white'
        : 'bg-(--q2-accent-soft) text-(--q2-accent-soft-text)'"
      :aria-pressed="activity.hasMyKudos"
      :aria-label="activity.hasMyKudos ? t.activity.takeBackKudos : t.activity.giveKudos"
      data-testid="kudos-button"
      data-q2-block
      @click="emit('kudos', activity.id)"
    >
      <UIcon
        name="i-lucide-hand-heart"
        class="size-4"
        aria-hidden="true"
      />
      <span class="text-xs font-extrabold">{{ activity.kudosCount }}</span>
    </button>

    <span
      v-else
      class="flex shrink-0 items-center gap-1 text-xs font-bold text-(--ui-text-muted)"
      data-q2-block
    >
      <UIcon
        name="i-lucide-hand-heart"
        class="size-4"
        aria-hidden="true"
      />
      {{ activity.kudosCount }}
    </span>
  </div>
</template>
