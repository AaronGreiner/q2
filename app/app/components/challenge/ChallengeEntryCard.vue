<script setup lang="ts">
import { hapticTap } from '~/utils/haptics'
import type { ChallengeEntry, KudosKind } from '~/api/types'
import { kudosKinds } from '~/api/types'
import { proofMediaOf } from '~/utils/proofMedia'

/**
 * One contribution in the challenge room.
 *
 * Deliberately plainer than a proof card: no verdict, no status, no deadline,
 * no second attempt. The challenge pays into no streak, so there is nothing to
 * protect and therefore nothing to check. What is left is the picture, who made
 * it, and applause.
 *
 * Two things about the covered state are rules rather than styling:
 *
 * - **The name is shown, the picture is not.** Seeing who is already in is the
 *   reason to join, and it costs nobody anything. Only the motif is held back.
 * - **There is nothing behind the cover to reveal.** The server does not send
 *   an image id until you have contributed, so this draws a placeholder rather
 *   than blurring a photograph it was given anyway — a blur is a curtain with a
 *   gap in it.
 *
 * An uncovered picture opens full screen with the day's prompt under it: the
 * card is shown below the prompt, the full-screen view is not. It may be a
 * clip, with the front camera's picture or clip in a corner that swaps and
 * moves, exactly like a proof's (`ProofMedia`, #152).
 */
const props = defineProps<{
  entry: ChallengeEntry
  /** Covered while the viewer has not contributed. */
  covered: boolean
  /** The day's challenge, for the full-screen view. */
  prompt?: string | null
}>()

const emit = defineEmits<{ react: [kind: KudosKind] }>()

const t = useMessages()
const now = useNow()

/** Nothing while covered: the server sent no picture to draw. */
const media = computed(() => (!props.covered && props.entry.imageId
  ? proofMediaOf({ ...props.entry, imageId: props.entry.imageId })
  : null))

const age = computed(() => formatRelativeTime(props.entry.createdAt, now.value, t.value))

/**
 * Only the exception is named — as a screenshot, the way a proof's is, and
 * the way the camera said it would be (#152).
 *
 * The origin weighs more here than on a proof: the prompt asks for a moment,
 * and a picture out of a gallery misses it by definition. It stays a label
 * rather than a refusal. A contribution from before the route was recorded
 * (`Unknown`, #64) is not called one: nobody knows that it was.
 */
const fromLibrary = computed(() => !props.covered && props.entry.source === 'Screenshot')

const viewer = usePhotoViewer()

function enlarge() {
  if (!media.value) return

  viewer.open({
    ...media.value,
    title: props.prompt,
    subtitle: props.entry.author.displayName,
    meta: age.value,
  })
}

function countOf(kind: KudosKind): number {
  return props.entry.reactions.find(reaction => reaction.kind === kind)?.count ?? 0
}

function isMine(kind: KudosKind): boolean {
  return props.entry.reactions.find(reaction => reaction.kind === kind)?.isMine ?? false
}
function react(kind: KudosKind) {
  hapticTap()
  emit('react', kind)
}
</script>

<template>
  <article
    class="q2-card overflow-hidden"
    data-testid="challenge-entry"
    data-q2-block
  >
    <header class="flex items-center gap-3 px-3.5 pt-3 pb-2.5">
      <AppAvatar
        :initials="entry.author.initials"
        :color="entry.author.avatarColor"
        :image-id="entry.author.avatarImageId"
        :size="32"
        :expand-title="entry.author.displayName"
      />

      <!-- The name, even on your own: the section above already says whose it
           is, and repeating "Dein Beitrag" inside the card said it twice. -->
      <p
        class="min-w-0 flex-1 truncate text-sm font-extrabold"
        data-q2-private
      >
        {{ entry.author.displayName }}
      </p>

      <span class="shrink-0 text-[11px] font-bold text-(--ui-text-dimmed)">
        <template v-if="entry.isLate">
          <span data-testid="challenge-entry-late">{{ t.challenge.late }}</span> ·
        </template>{{ age }}
      </span>
    </header>

    <!--
      A fixed 4:5 frame, so the column is the same height before and after the
      pictures arrive and nothing shifts under a thumb.
    -->
    <div class="relative aspect-4/5 w-full bg-(--ui-bg-elevated)">
      <template v-if="media">
        <ProofMedia
          :parts="media"
          mode="inline"
          openable
          :open-label="prompt ? t.challenge.enlarge(prompt) : t.proof.enlarge"
          :alt="t.challenge.entryAlt(entry.author.displayName)"
          data-testid="challenge-media"
          @open="enlarge()"
        />

        <ProofSourceBadge
          v-if="fromLibrary"
          source="Screenshot"
          class="pointer-events-none absolute start-2.5 bottom-2.5"
          data-testid="challenge-origin"
        />
      </template>

      <div
        v-else-if="covered"
        class="flex size-full flex-col items-center justify-center gap-1.5 text-(--ui-text-muted)"
        data-testid="challenge-covered"
      >
        <UIcon
          name="i-lucide-eye-off"
          class="size-5"
          aria-hidden="true"
        />
        <span class="text-[11px] font-bold">{{ t.challenge.coveredHint }}</span>
      </div>

      <div
        v-else
        class="flex size-full items-center justify-center"
      >
        <UIcon
          name="i-lucide-image-off"
          class="size-8 text-(--ui-text-dimmed)"
          aria-hidden="true"
        />
      </div>
    </div>

    <div
      v-if="!covered"
      class="flex flex-col gap-2 px-3.5 pt-2.5 pb-3"
    >
      <!--
        All three kinds are approving, which is what lets them be offered at
        all: there is no verdict here, so a reaction that could be negative
        would be the only way to be unpleasant in the room.
      -->
      <div class="flex items-center gap-1.5">
        <UButton
          v-for="kind in kudosKinds"
          :key="kind"
          class="min-h-11 min-w-11 justify-center"
          size="sm"
          color="neutral"
          :variant="isMine(kind) ? 'soft' : 'ghost'"
          :icon="kudosIconName(kind)"
          :label="countOf(kind) > 0 ? String(countOf(kind)) : undefined"
          :aria-label="t.kudos[kind]"
          :aria-pressed="isMine(kind)"
          :data-testid="`challenge-react-${kind}`"
          @click="react(kind)"
        />
      </div>
    </div>
  </article>
</template>
