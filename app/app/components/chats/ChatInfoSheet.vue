<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'
import type { ChatDetail, ChatPhoto } from '~/api/types'

/**
 * What is behind a conversation's header: who is in it, the goal it is about,
 * and leaving it.
 *
 * The header names the conversation; this says who "4 Mitglieder" are. Each
 * member goes to their profile — except the reader, who is listed as "Du" and
 * goes nowhere. A goal's conversation offers the way to the goal itself, which
 * is where its schedule, its history and its team are.
 *
 * Leaving lives here rather than as a second icon in the header: it is rare,
 * it cannot be undone, and next to the mute switch it was one tap from a
 * mistake. The page still asks before it happens.
 *
 * Pinning and the photographs live here too (#24): pinning is about this one
 * conversation and rare, and the photographs are what a thread read a page at
 * a time otherwise makes hard to find again.
 */
const props = defineProps<{
  chat: ChatDetail
  /** The signed-in person, who is listed as "Du" rather than as a link. */
  meId: string | null
  /** The conversation's photographs (#24); null until the page has read them. */
  photos?: ChatPhoto[] | null
  photosFailed?: boolean
  /** Why pinning was refused — three are pinned already, usually. */
  pinFailure?: ApiFailure | null
}>()

const emit = defineEmits<{ leave: [], pin: [pinned: boolean] }>()

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()

const pinned = computed({
  get: () => props.chat.isPinned,
  set: value => emit('pin', value),
})

/** Three pinned already is the usual refusal, and it gets its own sentence. */
function pinError(): string | undefined {
  const tooMany = Object.entries(props.pinFailure?.fieldCodes ?? {})
    .find(([field]) => field.toLowerCase() === 'pinned')?.[1]
    ?.find(code => code.code === 'tooMany')

  if (tooMany?.max !== undefined) return t.value.chats.pinTooMany(tooMany.max)
  return fieldErrorText(props.pinFailure, 'pinned', t.value)
}
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="chat.name"
    :description="t.chats.members(chat.memberCount)"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <div
        class="flex flex-col gap-4 pb-2"
        data-testid="chat-info"
      >
        <UButton
          v-if="chat.pinnedGoal"
          :to="`/goals/${chat.pinnedGoal.id}`"
          icon="i-lucide-target"
          color="neutral"
          variant="outline"
          size="lg"
          class="min-h-11 justify-center rounded-full font-extrabold"
          data-testid="chat-info-goal"
        >
          {{ t.chats.toGoal }}
        </UButton>

        <div
          class="flex items-center gap-3 px-1"
          data-testid="chat-info-pin"
        >
          <span class="min-w-0 flex-1">
            <span class="block text-sm font-bold">{{ t.chats.pin }}</span>
            <span
              class="block text-xs font-semibold"
              :class="pinFailure ? 'text-(--ui-error)' : 'text-(--ui-text-muted)'"
              :role="pinFailure ? 'alert' : undefined"
              data-testid="chat-info-pin-hint"
            >{{ pinError() ?? t.chats.pinHint }}</span>
          </span>
          <AppToggle
            v-model="pinned"
            :label="t.chats.pin"
          />
        </div>

        <section
          aria-labelledby="chat-info-photos"
          data-testid="chat-info-photos"
        >
          <h3
            id="chat-info-photos"
            class="q2-eyebrow mb-2 px-0.5"
          >
            {{ t.chats.photosHeading }}
          </h3>

          <p
            v-if="photosFailed"
            class="px-0.5 text-sm font-semibold text-(--ui-text-muted)"
          >
            {{ t.chats.photosFailed }}
          </p>

          <div
            v-else-if="photos === null || photos === undefined"
            class="grid grid-cols-3 gap-1.5"
            aria-busy="true"
          >
            <USkeleton
              v-for="index in 3"
              :key="index"
              class="aspect-square w-full rounded-(--q2-radius-sm)"
            />
          </div>

          <p
            v-else-if="photos.length === 0"
            class="px-0.5 text-sm font-semibold text-(--ui-text-muted)"
          >
            {{ t.chats.photosEmpty }}
          </p>

          <ul
            v-else
            class="grid list-none grid-cols-3 gap-1.5 p-0"
          >
            <li
              v-for="photo in photos"
              :key="photo.id"
            >
              <ChatPhotoTile
                :photo="photo"
                :context="chat.name"
              />
            </li>
          </ul>
        </section>

        <section aria-labelledby="chat-info-members">
          <h3
            id="chat-info-members"
            class="q2-eyebrow mb-2 px-0.5"
          >
            {{ t.chats.infoMembers }}
          </h3>

          <ul
            class="flex list-none flex-col p-0"
            data-q2-private
          >
            <li
              v-for="member in chat.members"
              :key="member.id"
              data-testid="chat-info-member"
            >
              <div
                v-if="member.id === meId"
                class="flex min-h-12 items-center gap-3 px-1 py-1.5"
              >
                <AppAvatar
                  :initials="member.initials"
                  :color="member.avatarColor"
                  :image-id="member.avatarImageId"
                  :size="36"
                />
                <span class="min-w-0 flex-1 truncate text-sm font-bold">{{ t.chats.you }}</span>
              </div>

              <NuxtLink
                v-else
                :to="`/people/${member.id}`"
                class="flex min-h-12 items-center gap-3 rounded-(--q2-radius-md) px-1 py-1.5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
              >
                <AppAvatar
                  :initials="member.initials"
                  :color="member.avatarColor"
                  :image-id="member.avatarImageId"
                  :online="member.isOnline"
                  :size="36"
                />

                <span class="min-w-0 flex-1">
                  <span class="block truncate text-sm font-bold">{{ member.displayName }}</span>
                  <span class="block truncate text-xs font-semibold text-(--ui-text-muted)">{{ member.handle }}</span>
                </span>

                <UIcon
                  name="i-lucide-chevron-right"
                  class="size-4 shrink-0 text-(--ui-text-dimmed)"
                  aria-hidden="true"
                />
              </NuxtLink>
            </li>
          </ul>
        </section>

        <!-- Only a group can be left; a direct conversation is between the two
             of you, and a goal's is left by leaving the goal. Red, because it
             is final. -->
        <UButton
          v-if="chat.kind === 'Group'"
          icon="i-lucide-log-out"
          color="error"
          variant="soft"
          size="lg"
          class="min-h-11 justify-center rounded-full font-extrabold"
          data-testid="leave-group"
          @click="emit('leave')"
        >
          {{ t.chats.leaveGroup }}
        </UButton>
      </div>
    </template>
  </UDrawer>
</template>
