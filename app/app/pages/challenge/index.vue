<script setup lang="ts">
import type { Image, KudosKind } from '~/api/types'

/**
 * The room for today's prompt.
 *
 * Not a chat, although it feels like one: nothing is written here, things are
 * shown and applauded. That is also why it is not in the chats tab — a row in
 * the chat list would promise a text field that does not exist.
 *
 * Everybody sees a different room, and this page does not decide which. Whose
 * contributions are in it, and whether their pictures come with them, are the
 * server's answers (`ChallengeService`); the page draws what it is given.
 */
definePageMeta({ layout: 'plain' })

const t = useMessages()

const {
  room,
  error,
  isLoading,
  isSubmitting,
  refresh,
  contribute,
  withdraw,
  react,
  maxEdge,
} = useChallengeRoom()

const isCapturing = ref(false)
const isConfirmingRemoval = ref(false)

const covered = computed(() => room.value !== null && !room.value.isRevealed)

/*
 * Pulled out of the template rather than reached through `room` there: a
 * template cannot narrow `room.ownEntry` inside a nested handler, and a
 * non-null assertion in markup is a claim nothing checks.
 */
const ownEntry = computed(() => room.value?.ownEntry ?? null)

/*
 * A named handler rather than an inline arrow.
 *
 * A `v-if` narrows the template around it but not inside a callback written in
 * it, so `ownEntry.id` there is a claim the compiler cannot check — and the day
 * the guard moves, it would be wrong at runtime as well.
 */
function reactToOwn(kind: KudosKind) {
  if (ownEntry.value) react(ownEntry.value.id, kind)
}

const participation = computed(() => {
  if (!room.value) return ''

  return room.value.friendCount === 0
    ? t.value.challenge.nobody
    : t.value.challenge.participation(room.value.entries.length, room.value.friendCount)
})

async function onCaptured(image: Image) {
  isCapturing.value = false
  await contribute(image)
}

useHead({ title: () => t.value.challenge.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader
      :title="t.challenge.heading"
      back-to="/"
      :back-label="t.common.back"
    >
      <template #actions>
        <!--
          Outside the loading state: the archive is reachable even on a day
          with no challenge running.
        -->
        <UButton
          to="/challenge/archive"
          icon="i-lucide-package-open"
          color="neutral"
          variant="outline"
          size="lg"
          :ui="{ base: 'size-11 justify-center rounded-full' }"
          :aria-label="t.challenge.archive"
          data-testid="challenge-archive-link"
        />
      </template>
    </AppScreenHeader>

    <div class="q2-scroll flex-1 px-[18px] pt-1.5 pb-6">
      <div
        v-if="isLoading"
        class="flex flex-col gap-3"
        aria-busy="true"
        aria-live="polite"
      >
        <span class="sr-only">{{ t.common.loading }}</span>
        <USkeleton class="h-16 w-full rounded-(--q2-radius-lg)" />
        <USkeleton class="aspect-4/5 w-full rounded-(--q2-radius-lg)" />
      </div>

      <AppErrorState
        v-else-if="error"
        :error="error"
        retryable
        @retry="refresh()"
      />

      <template v-else-if="room">
        <!-- The prompt is the most important thing on the page, and is sized
             accordingly. -->
        <section>
          <h2
            class="text-xl leading-snug font-extrabold tracking-tight"
            data-testid="challenge-prompt"
          >
            {{ room.challenge.prompt }}
          </h2>
          <p class="mt-2 text-[12px] font-semibold text-(--ui-text-muted)">
            {{ participation }}
          </p>
        </section>

        <UButton
          v-if="!room.ownEntry"
          class="mt-4"
          block
          size="xl"
          icon="i-lucide-camera"
          :label="t.challenge.join"
          :loading="isSubmitting"
          data-testid="challenge-join"
          @click="isCapturing = true"
        />

        <!-- Your own first: it is the key to the rest of the page. -->
        <section
          v-if="ownEntry"
          class="mt-5"
          aria-labelledby="challenge-own-heading"
        >
          <div class="mb-2 flex items-center gap-2">
            <h3
              id="challenge-own-heading"
              class="q2-eyebrow min-w-0 flex-1"
            >
              {{ t.challenge.yours }}
            </h3>

            <UButton
              class="-my-2 min-h-11 py-2"
              size="sm"
              color="neutral"
              variant="ghost"
              :label="t.challenge.replace"
              :disabled="isSubmitting"
              data-testid="challenge-replace"
              @click="isCapturing = true"
            />

            <UButton
              class="-my-2 min-h-11 py-2"
              size="sm"
              color="error"
              variant="ghost"
              :label="t.challenge.remove"
              :disabled="isSubmitting"
              data-testid="challenge-remove"
              @click="isConfirmingRemoval = true"
            />
          </div>

          <ChallengeEntryCard
            :entry="ownEntry"
            :covered="false"
            @react="reactToOwn"
          />
        </section>

        <section
          v-if="room.entries.length > 0"
          class="mt-6"
          aria-labelledby="challenge-friends-heading"
        >
          <h3
            id="challenge-friends-heading"
            class="q2-eyebrow mb-2"
          >
            {{ covered ? t.challenge.covered : t.challenge.friends }}
          </h3>

          <div class="flex flex-col gap-3">
            <ChallengeEntryCard
              v-for="entry in room.entries"
              :key="entry.id"
              :entry="entry"
              :covered="covered"
              @react="kind => react(entry.id, kind)"
            />
          </div>
        </section>

        <AppStateMessage
          v-else
          class="mt-6"
          icon="i-lucide-users"
          :title="t.challenge.noFriendsYet"
          data-testid="challenge-no-friends"
        />
      </template>

      <AppStateMessage
        v-else
        icon="i-lucide-zap"
        :title="t.challenge.none"
        :description="t.challenge.noneHint"
        data-testid="challenge-none"
      />
    </div>

    <!--
      One camera for the whole screen. Opened from the page rather than from
      inside another sheet: two drawers open at once deadlock — see
      PhotoCapture.
    -->
    <PhotoCapture
      v-model:open="isCapturing"
      purpose="ChallengeEntry"
      :max-edge="maxEdge"
      @uploaded="onCaptured"
    />

    <!-- Asked first, because covering the friends' pictures again is the real
         consequence and has to be clear beforehand. -->
    <AppConfirmDialog
      v-model:open="isConfirmingRemoval"
      :title="t.challenge.removeHeading"
      :description="t.challenge.removeBody"
      :confirm-label="t.challenge.removeConfirm"
      @confirm="withdraw()"
    />
  </div>
</template>
