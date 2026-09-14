<script setup lang="ts">
import type { NotificationLine } from '~/api/types'

/**
 * One line in the bell.
 *
 * Who, and what, in the reader's language — composed from the kind and its
 * parts by the same function the service worker writes a lock screen with, so
 * the two cannot say one event two ways (`notificationText`).
 *
 * The whole row is the link: a line in the bell is there to take somebody to
 * what it is about. What is new is marked with weight and a dot in the text
 * colour. It is a state, so no accent (docs/adr/0015-qdos-design-language.md).
 */
const props = defineProps<{
  line: NotificationLine
  /** Shared clock, so SSR and the browser agree on "vor 12 Min". */
  now: number
}>()

const t = useMessages()

const text = computed(() => notificationText(props.line, t.value))
const to = computed(() => notificationLink(props.line))
const icon = computed(() => notificationIcon(props.line))
const time = computed(() => formatRelativeTime(props.line.occurredAt, props.now, t.value))
</script>

<template>
  <NuxtLink
    :to="to"
    class="q2-card flex items-center gap-3 px-3 py-3 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    data-testid="notification-row"
  >
    <AppAvatar
      v-if="line.actor"
      :initials="line.actor.initials"
      :color="line.actor.avatarColor"
      :image-id="line.actor.avatarImageId"
      :size="40"
    />

    <!-- Nobody to picture: a verdict, a lifted pause. Anonymous by design,
         so the tile is an icon on a neutral surface rather than a face. -->
    <span
      v-else
      class="flex size-10 shrink-0 items-center justify-center rounded-full bg-(--ui-bg-accented) text-(--ui-text)"
      aria-hidden="true"
    >
      <UIcon
        :name="icon"
        class="size-5"
      />
    </span>

    <div
      class="min-w-0 flex-1"
      data-q2-private
    >
      <!-- A name, and what they did to something of yours — which quotes a
           goal title, so the whole line is private. -->
      <p
        class="text-[13px] leading-snug"
        :class="line.isNew ? 'text-(--ui-text)' : 'text-(--ui-text-muted)'"
      >
        <b
          v-if="text.who"
          class="font-extrabold"
        >{{ text.who }}</b> {{ text.text }}
      </p>
      <p class="mt-0.5 text-[11px] font-semibold text-(--ui-text-dimmed)">
        {{ time }}
      </p>
    </div>

    <span
      v-if="line.isNew"
      class="size-2 shrink-0 rounded-full bg-(--ui-text)"
      data-testid="notification-new"
    >
      <span class="sr-only">{{ t.notify.new }}</span>
    </span>
  </NuxtLink>
</template>
