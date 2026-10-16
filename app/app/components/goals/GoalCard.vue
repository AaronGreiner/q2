<script setup lang="ts">
import type { Goal } from '~/api/types'

/**
 * One goal in the list, as a row you open rather than a row you act on.
 *
 * The reference component for this codebase: typed props, everything it shows
 * derived from them, no state, no fetching, and it can be rendered in a test
 * from a plain object. Navigation belongs to the page composing it — which is
 * why the whole row is a link rather than a div with a click handler.
 *
 * The same shape as the rows that carry an action (`GoalWindowRow`,
 * `DontWindowRow`), because the Dos & Don'ts list puts them one under the
 * other (#77). It carries no control: your own window that covers today is
 * drawn by one of those rows instead, and a friend's goal is theirs to deliver
 * — the server refuses anybody else's photograph.
 *
 * **Whose it is decides what leads the row.** Your own goal is led by its
 * icon; a friend's by the friend, with their name on the line under the
 * title, because on that row "who" is the first question and the icon only
 * the second.
 */
const props = withDefaults(defineProps<{
  goal: Goal
  /**
   * Today as the reader's local day (`YYYY-MM-DD`), for a Don't's "seit X
   * Tagen". Passed in, like every clock here, so the row stays a function of
   * its props.
   */
  today?: string | null
}>(), {
  today: null,
})

const t = useMessages()

const isDont = computed(() => props.goal.kind === 'Dont')
const schedule = computed(() => goalScheduleLabel(props.goal, t.value))

/**
 * A Don't shows its clean run where a Do shows its streak: "Seit 12 Tagen" is
 * what somebody who stopped something counts, and a relapse is the break in it.
 */
const run = computed(() => (isDont.value && props.goal.runStartedOn && props.today
  ? t.value.dont.run(runDays(props.goal.runStartedOn, props.today))
  : null))

/**
 * The flame only for a streak that exists. "0 Fenster" in the streak colour
 * was the gradient spent on nothing (docs/adr/0015-qdos-design-language.md).
 */
const streak = computed(() => (!isDont.value && props.goal.streak > 0
  ? t.value.goals.streakDays(props.goal.streak)
  : null))

const reminder = computed(() => formatClock(props.goal.reminderAt))

/**
 * Where the open window stands, in words and, where the words are a state
 * rather than a deadline, an icon. A paused goal has no open window, and
 * "Abgeschlossen" would be a lie about a goal that is merely resting.
 *
 * "Wird geprüft" and "Geschafft" are said here and not only on "Heute": on a
 * friend's row they are the whole reason to look.
 */
const state = computed<{ text: string, icon: string | null }>(() => {
  const window = props.goal.current

  if (!window) {
    return {
      text: props.goal.pause ? t.value.pause.until(formatDay(props.goal.pause.endsOn)) : t.value.status[props.goal.status],
      icon: null,
    }
  }

  // A window that has not begun is open on the server all the same, and
  // `windowLabel` would call Sunday's run "Heute fällig" on a Monday.
  if (props.today && window.startsOn > props.today) {
    const day = formatDay(window.startsOn)
    return { text: window.startsOn === window.dueOn ? t.value.window.on(day) : t.value.window.from(day), icon: null }
  }

  if (window.status === 'Relapse') return { text: t.value.window.relapse, icon: 'i-lucide-rotate-ccw' }
  if (window.pendingProofId) return { text: t.value.proof.waiting, icon: 'i-lucide-hourglass' }
  if (!isDont.value && window.remainingProofs === 0) return { text: t.value.window.done, icon: 'i-lucide-check' }

  return { text: windowLabel(window, t.value), icon: null }
})
</script>

<template>
  <NuxtLink
    :to="`/goals/${goal.id}`"
    class="q2-press q2-card flex items-center gap-3 px-3.5 py-3 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    data-testid="goal-card"
  >
    <AppAvatar
      v-if="!goal.isMine"
      :initials="goal.owner.initials"
      :color="goal.owner.avatarColor"
      :image-id="goal.owner.avatarImageId"
      :size="36"
      data-testid="goal-owner-avatar"
    />

    <span
      v-else
      class="flex size-9 shrink-0 items-center justify-center rounded-(--q2-radius-sm) bg-(--ui-bg-accented) text-(--ui-text)"
      aria-hidden="true"
      data-q2-block
    >
      <UIcon
        :name="goalIconName(goal.icon)"
        class="size-5"
      />
    </span>

    <div class="min-w-0 flex-1">
      <!-- h3: the list sits under a section's h2, which keeps heading order intact. -->
      <h3
        class="text-sm font-bold text-pretty text-(--ui-text)"
        data-q2-private
      >
        {{ goal.title }}
      </h3>

      <p
        v-if="!goal.isMine"
        class="mt-0.5 truncate text-xs font-semibold text-(--ui-text-toned)"
        data-testid="goal-owner"
        data-q2-private
      >
        {{ goal.owner.displayName }}
      </p>

      <div class="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1">
        <!-- The chip says which kind it is, the same way "Heute" does: a
             repeat for something to do, a ban for something to stop. -->
        <span
          class="inline-flex items-center gap-1 rounded-full bg-(--ui-bg-accented) px-2 py-0.5 text-[11px] font-bold text-(--ui-text-toned)"
          data-testid="goal-schedule"
          data-q2-private
        >
          <UIcon
            :name="isDont ? 'i-lucide-ban' : 'i-lucide-repeat'"
            class="size-3"
            aria-hidden="true"
          />
          {{ schedule }}
        </span>

        <span
          class="inline-flex items-center gap-1 text-[11px] font-bold text-(--ui-text-muted)"
          data-testid="goal-window"
          data-q2-private
        >
          <UIcon
            v-if="state.icon"
            :name="state.icon"
            class="size-3"
            aria-hidden="true"
          />
          {{ state.text }}
        </span>

        <span
          v-if="goal.isGroup"
          class="rounded-full bg-(--ui-bg-accented) px-1.5 py-0.5 text-[10px] font-extrabold text-(--ui-text-toned)"
        >
          {{ t.goals.group }}
        </span>

        <span
          v-if="goal.isOverdue"
          class="rounded-full bg-(--q2-flame-soft) px-1.5 py-0.5 text-[10px] font-extrabold text-(--q2-flame-text)"
          data-testid="goal-overdue"
        >
          {{ t.goals.overdue }}
        </span>

        <!-- Grey, and never the flame. A pause is not a failure and not
             something to do; it is the absence of both. -->
        <span
          v-if="goal.pause"
          class="rounded-full bg-(--ui-bg-accented) px-1.5 py-0.5 text-[10px] font-extrabold text-(--ui-text-toned)"
          data-testid="goal-paused"
        >
          {{ t.pause.bannerTitle }}
        </span>

        <span
          v-if="reminder && goal.isMine"
          class="flex items-center gap-1 text-[11px] font-semibold text-(--ui-text-dimmed)"
          data-testid="goal-reminder"
        >
          <UIcon
            name="i-lucide-bell"
            class="size-3"
            aria-hidden="true"
          />
          <span aria-hidden="true">{{ reminder }}</span>
          <span class="sr-only">{{ t.window.reminderAt(reminder) }}</span>
        </span>
      </div>
    </div>

    <span
      v-if="run"
      class="flex shrink-0 items-center gap-1 text-[11px] font-bold text-(--ui-text-toned)"
      data-testid="goal-run"
      data-q2-private
    >
      <UIcon
        name="i-lucide-shield-check"
        class="size-3.5"
        aria-hidden="true"
      />
      {{ run }}
    </span>

    <span
      v-else-if="streak"
      class="flex shrink-0 items-center gap-1 text-[11px] font-bold text-(--q2-flame-text)"
      data-testid="goal-streak"
      data-q2-private
    >
      <UIcon
        name="i-lucide-flame"
        class="size-3.5"
        aria-hidden="true"
      />
      {{ streak }}
    </span>
  </NuxtLink>
</template>
