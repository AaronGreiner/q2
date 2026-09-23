<script setup lang="ts">
import { hapticTap } from '~/utils/haptics'
import { imageUrl } from '~/api/images'
import type { ChatMessage, KudosKind } from '~/api/types'
import { kudosKinds } from '~/api/types'

/**
 * One message, and the kudos on it.
 *
 * All three kinds are offered rather than one, because "well done" has more
 * than one register and the whole product is built on people telling each other
 * they saw it. They are icons, never emoji, and the one you gave is marked by a
 * filled surface rather than by a second colour — a reaction already given is a
 * state, and the accent belongs to actions.
 */
const props = defineProps<{
  message: ChatMessage
  now: number
}>()

const emit = defineEmits<{ react: [messageId: string, kind: KudosKind] }>()

const t = useMessages()

const zone = useTimeZoneOffset()

const time = computed(() => formatClock(props.message.sentAt, zone.value))

const { public: config } = useRuntimeConfig()
const photoFailed = ref(false)

watch(() => props.message.image?.id, () => {
  photoFailed.value = false
})

const photoSource = computed(() =>
  props.message.image && !photoFailed.value ? imageUrl(config.apiBaseUrl, props.message.image.id) : null)

/*
 * The frame is reserved at the picture's own proportions before a byte of it
 * arrives, so the thread does not jump under the reader as it loads — clamped
 * between 3:4 and 3:2, so a panorama is not a sliver and a tall screenshot not
 * a whole screen.
 */
const photoRatio = computed(() => {
  const image = props.message.image
  if (!image || image.height <= 0) return 1

  return Math.min(1.5, Math.max(0.75, image.width / image.height))
})

/** A photograph deleted since, with no words beside it, still gets a bubble. */
const photoGone = computed(() => !props.message.image && props.message.text.length === 0)

/** How many of each kind, and whether one of them is mine. */
function reactionFor(kind: KudosKind) {
  return props.message.reactions.find(reaction => reaction.kind === kind)
}
function react(kind: KudosKind) {
  hapticTap()
  emit('react', props.message.id, kind)
}
</script>

<template>
  <li
    class="flex max-w-[80%] list-none flex-col"
    :class="message.isMine ? 'items-end self-end' : 'items-start self-start'"
    data-testid="chat-bubble"
    data-q2-block
  >
    <span
      v-if="message.senderName"
      class="mb-0.5 ms-1 text-[11px] font-extrabold text-(--ui-text-muted)"
    >{{ message.senderName }}</span>

    <!--
      Your own message is a raised grey, not the accent. A thread is mostly your
      own messages, and painting all of them the accent would make the colour
      mean "mine" instead of "something you can do".
    -->
    <!-- A photograph sits above its words, with the bubble's own corner. -->
    <div
      v-if="message.image"
      class="relative w-64 max-w-full overflow-hidden bg-(--ui-bg-elevated)"
      :class="[
        message.isMine ? 'rounded-(--q2-radius-lg) rounded-ee-[5px]' : 'rounded-(--q2-radius-lg) rounded-es-[5px]',
        message.text ? 'mb-1' : '',
      ]"
      :style="{ aspectRatio: String(photoRatio) }"
      data-testid="chat-photo"
    >
      <img
        v-if="photoSource"
        :src="photoSource"
        :alt="t.chats.photo"
        crossorigin="use-credentials"
        decoding="async"
        loading="lazy"
        class="size-full object-cover"
        @error="photoFailed = true"
      >

      <div
        v-else
        class="flex size-full items-center justify-center"
      >
        <UIcon
          name="i-lucide-image-off"
          class="size-8 text-(--ui-text-dimmed)"
          aria-hidden="true"
        />
        <span class="sr-only">{{ t.chats.photoGone }}</span>
      </div>
    </div>

    <p
      v-if="message.text || photoGone"
      class="px-3.5 py-2.5 text-sm leading-snug font-medium"
      :class="[
        message.isMine
          ? 'rounded-(--q2-radius-lg) rounded-ee-[5px] bg-(--ui-bg-accented) text-(--ui-text)'
          : 'bg-(--q2-track) rounded-(--q2-radius-lg) rounded-es-[5px] text-(--ui-text)',
        photoGone ? 'inline-flex items-center gap-1.5 text-(--ui-text-muted)' : '',
      ]"
    >
      <template v-if="photoGone">
        <UIcon
          name="i-lucide-image-off"
          class="size-4 shrink-0"
          aria-hidden="true"
        />
        {{ t.chats.photoGone }}
      </template>
      <template v-else>
        {{ message.text }}
      </template>
    </p>

    <div
      class="mt-1 flex items-center gap-1.5 px-1"
      :class="message.isMine ? 'flex-row-reverse' : ''"
    >
      <!-- Your own message shows what came back; somebody else's is also a
           control, because the reply to a message is often just a reaction. -->
      <template v-if="message.isMine">
        <span
          v-for="reaction in message.reactions"
          :key="reaction.kind"
          class="inline-flex items-center gap-1 rounded-full border border-(--ui-border) bg-(--q2-surface) px-2 py-0.5 text-[11px] font-bold"
        >
          <UIcon
            :name="kudosIconName(reaction.kind)"
            class="size-3.5"
            aria-hidden="true"
          />
          <span class="sr-only">{{ t.kudos[reaction.kind] }}</span>
          {{ reaction.count }}
        </span>
      </template>

      <!--
        Kudos nobody has given yet are a bare icon rather than an outlined pill:
        three empty pills under every message is a row of controls where there
        should be a conversation. Giving one fills it.
      -->
      <template v-else>
        <button
          v-for="kind in kudosKinds"
          :key="kind"
          type="button"
          class="q2-press q2-reaction relative inline-flex items-center gap-1 rounded-full py-0.5 text-[11px] font-bold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
          :class="reactionFor(kind)?.isMine
            ? 'bg-(--q2-accent-solid) px-2 text-(--q2-accent-contrast)'
            : reactionFor(kind)
              ? 'border border-(--ui-border) bg-(--q2-surface) px-2 text-(--ui-text-muted)'
              : 'px-1.5 text-(--ui-text-dimmed)'"
          :aria-pressed="reactionFor(kind)?.isMine ?? false"
          :aria-label="t.kudos[kind]"
          :data-testid="`chat-kudos-${kind}`"
          @click="react(kind)"
        >
          <UIcon
            :name="kudosIconName(kind)"
            class="size-3.5"
            aria-hidden="true"
          />
          <span v-if="reactionFor(kind)">{{ reactionFor(kind)!.count }}</span>
        </button>
      </template>

      <span class="text-[10px] font-semibold text-(--ui-text-dimmed)">{{ time }}</span>
    </div>
  </li>
</template>
