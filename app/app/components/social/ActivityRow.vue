<script setup lang="ts">
import { NuxtLink } from '#components'
import { hapticTap } from '~/utils/haptics'
import type { Activity } from '~/api/types'

/**
 * One line of the friends' feed, with its kudos button.
 *
 * The sentence is composed here from the structured event rather than sent as
 * text — see `activitySentence` and the server's ActivityResponse for why.
 *
 * Every line in the feed is good news, so every line can be cheered. The one
 * that was not — a friend about to miss — is a notification to that friend's
 * friends now, where there is no button to press under it
 * (docs/adr/0024-one-notification-pipeline.md).
 *
 * The line opens what it is about (#103): the goal, when the server says the
 * reader may open it (`goalId`), and otherwise the person. The avatar opens
 * the person either way; when the line goes there too, the avatar is the same
 * target twice and is kept out of the tab order and the accessibility tree.
 * Your own history (`readonly`) never links to yourself.
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

const personLink = computed(() => (props.readonly ? null : `/people/${props.activity.actor.id}`))
const lineLink = computed(() => (props.activity.goalId ? `/goals/${props.activity.goalId}` : personLink.value))

/** What the kudos button is about, so a list of them is not five identical names. */
const line = computed(() => `${props.activity.actor.displayName} ${sentence.value}`)
function giveKudos() {
  hapticTap()
  emit('kudos', props.activity.id)
}
</script>

<template>
  <div
    class="q2-card flex items-center gap-3 px-3 py-3"
    data-testid="activity-row"
  >
    <NuxtLink
      v-if="personLink"
      :to="personLink"
      class="-m-0.5 flex size-11 shrink-0 items-center justify-center rounded-full focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :aria-label="lineLink === personLink ? undefined : t.activity.openPerson(activity.actor.displayName)"
      :aria-hidden="lineLink === personLink ? 'true' : undefined"
      :tabindex="lineLink === personLink ? -1 : undefined"
      data-testid="activity-person"
      data-q2-private
    >
      <AppAvatar
        :initials="activity.actor.initials"
        :color="activity.actor.avatarColor"
        :image-id="activity.actor.avatarImageId"
        :size="40"
      />
    </NuxtLink>

    <AppAvatar
      v-else
      :initials="activity.actor.initials"
      :color="activity.actor.avatarColor"
      :image-id="activity.actor.avatarImageId"
      :size="40"
    />

    <component
      :is="lineLink ? NuxtLink : 'div'"
      :to="lineLink ?? undefined"
      class="flex min-h-11 min-w-0 flex-1 flex-col justify-center"
      :class="lineLink && 'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)'"
      :data-testid="lineLink ? 'activity-link' : undefined"
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
    </component>

    <button
      v-if="!readonly"
      type="button"
      class="q2-press q2-reaction relative flex shrink-0 items-center gap-1 rounded-full px-3 py-1.5 transition-colors after:absolute after:-inset-y-2 after:-inset-x-1 after:content-[''] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
      :class="activity.hasMyKudos
        ? 'bg-(--q2-accent-solid) text-(--q2-accent-contrast)'
        : 'bg-(--q2-accent-soft) text-(--q2-accent-soft-text)'"
      :aria-pressed="activity.hasMyKudos"
      :aria-label="activity.hasMyKudos ? t.activity.takeBackKudos(line) : t.activity.giveKudos(line)"
      data-testid="kudos-button"
      data-q2-block
      @click="giveKudos"
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
