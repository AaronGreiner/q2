<script setup lang="ts">
import { hapticTap } from '~/utils/haptics'
import type { DoubtReason, Proof, ProofVoteValue } from '~/api/types'
import { doubtReasons } from '~/api/types'

/**
 * Somebody's photograph, and the verdict on it.
 *
 * The card the whole product turns on, and the one screen where q2 stops
 * looking like a habit tracker: a friend's picture, what they promised, and two
 * buttons that decide whether it counted.
 *
 * Three things about it are rules rather than layout, and each one is in the
 * design because the alternative breaks the check:
 *
 * - **Confirm is the larger button and comes first.** Believing a friend is the
 *   ordinary answer; doubting has to be a deliberate second choice rather than
 *   a symmetrical one. Two equal buttons would make doubt feel like a coin
 *   toss.
 * - **Doubt is named as anonymous, out loud.** People will not doubt honestly
 *   unless they know it cannot be traced, so the card says so where the button
 *   is rather than burying it.
 * - **"Live" or "Screenshot" is stated.** A picture chosen from a gallery can
 *   be any age, and the person voting is entitled to know which they are
 *   looking at. It is a label, never a refusal — some commitments only show on
 *   a screen, and a phone whose camera permission was denied still has to be
 *   able to deliver (#61). A screenshot is tall and full of text, so it is
 *   fitted into the square rather than cropped.
 *
 * A vote already cast can be changed from here until the photograph is
 * decided — the server says when with `canIVote`. It is one quiet button under
 * the verdict rather than the two big ones again: changing your mind is the
 * exception, and the card should not look undecided once you have decided.
 *
 * The picture opens full screen on a tap, because a proof is judged on detail
 * and a square in a feed is not enough to see it.
 *
 * A doubt can say what was missing (#111): "Anzweifeln" asks which of three
 * fixed answers, or none, and a doubt already cast can be given one or have
 * it changed. Anonymous like the doubt — the owner of the photograph reads
 * them counted, and nobody else reads them at all. A swipe in the stack
 * doubts without a reason, the quick way.
 */
const props = withDefaults(defineProps<{
  proof: Proof
  /** What was promised, so the picture can be judged against something. */
  goalTitle?: string | null
  /** What counts, in the owner's words, when they said (#95). Shown with the title. */
  goalDescription?: string | null
  /**
   * What the picture is of, for the full-screen view only — in a goal's
   * conversation the card does not name the goal, because the thread does.
   */
  context?: string | null
  busy?: boolean
}>(), {
  goalTitle: null,
  goalDescription: null,
  context: null,
  busy: false,
})

const emit = defineEmits<{ vote: [value: ProofVoteValue, reason: DoubtReason | null] }>()

const t = useMessages()
const now = useNow()
const picture = useImageSource(() => props.proof.imageId)

const failed = ref(false)

watch(() => props.proof.imageId, () => {
  failed.value = false
})

const source = computed(() => (failed.value ? null : picture.value))

/** Whole hours left, floored — "noch 2 Stunden" must never read as more than there is. */
const hoursLeft = computed(() => {
  // `useNow` is epoch millis, shared across the app so the server and the
  // browser agree on what "now" was at hydration.
  const remaining = new Date(props.proof.expiresAt).getTime() - now.value

  return remaining <= 0 ? null : Math.floor(remaining / 3_600_000)
})

const confirmedNames = computed(() =>
  props.proof.votes.confirmedBy.map(person => person.displayName).join(', '))

/** The other answer, for somebody who has voted and may still change it. */
const otherVote = computed<ProofVoteValue>(() => (props.proof.votes.myVote === 'Confirm' ? 'Doubt' : 'Confirm'))

const viewer = usePhotoViewer()

function enlarge() {
  viewer.open({
    imageId: props.proof.imageId,
    title: props.goalTitle ?? props.context,
    subtitle: props.proof.uploader.displayName,
    meta: formatRelativeTime(props.proof.createdAt, now.value, t.value),
    source: props.proof.source,
  })
}

/** Between "Anzweifeln" and the answer to "was fehlt?". */
const choosingReason = ref(false)

watch(() => props.proof.id, () => {
  choosingReason.value = false
})

function vote(value: ProofVoteValue, reason: DoubtReason | null = null) {
  hapticTap()
  choosingReason.value = false
  emit('vote', value, reason)
}

/** The reasons the owner's doubters gave, worded and counted — empty for anybody else. */
const reasonsText = computed(() => props.proof.votes.doubtReasons
  .flatMap(reason => (reason.reason ? [t.value.proof.reasonCount(t.value.proof.doubtReason[reason.reason], reason.count)] : []))
  .join(' · '))
</script>

<template>
  <article
    class="q2-card overflow-hidden"
    data-testid="proof-card"
    data-q2-block
  >
    <header class="flex items-center gap-3 px-3.5 pt-3 pb-2.5">
      <AppAvatar
        :initials="proof.uploader.initials"
        :color="proof.uploader.avatarColor"
        :image-id="proof.uploader.avatarImageId"
        :size="36"
        :expand-title="proof.uploader.displayName"
      />

      <div class="min-w-0 flex-1">
        <p
          class="truncate text-sm font-extrabold"
          data-q2-private
        >
          {{ proof.uploader.displayName }}
        </p>
        <p
          v-if="goalTitle"
          class="truncate text-[12px] font-semibold text-(--ui-text-muted)"
          data-q2-private
        >
          {{ goalTitle }}
        </p>
      </div>

      <!-- Only while the vote runs. In a goal's conversation older photographs
           stay in the thread, and a decided one has no deadline left to show. -->
      <template v-if="proof.status === 'Voting'">
        <span
          v-if="hoursLeft !== null"
          class="shrink-0 text-[11px] font-bold text-(--ui-text-dimmed)"
          data-testid="proof-expiry"
        >{{ t.proof.expiresIn(hoursLeft) }}</span>
        <span
          v-else
          class="shrink-0 text-[11px] font-bold text-(--ui-text-dimmed)"
        >{{ t.proof.expired }}</span>
      </template>
    </header>

    <!-- The agreement the picture is judged against. Three lines at most, so
         the card keeps its height in the stack; the whole text is on the
         goal's screen. -->
    <p
      v-if="goalTitle && goalDescription"
      class="line-clamp-3 px-3.5 pb-2.5 text-[13px] leading-snug font-semibold whitespace-pre-line text-(--ui-text-muted)"
      data-q2-private
      data-testid="proof-goal-description"
    >
      {{ goalDescription }}
    </p>

    <!--
      A square frame at a fixed ratio, so the card is the same height before and
      after the picture arrives. A feed that reflows as each photograph loads is
      a feed somebody votes on by accident.
    -->
    <div class="relative aspect-square w-full bg-(--ui-bg-elevated)">
      <button
        v-if="source"
        type="button"
        class="block size-full focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-(--ui-primary)"
        :aria-label="t.proof.enlarge"
        data-testid="proof-enlarge"
        @click="enlarge()"
      >
        <img
          crossorigin="use-credentials"
          :src="source"
          alt=""
          decoding="async"
          draggable="false"
          class="size-full"
          :class="proof.source === 'Screenshot' ? 'object-contain' : 'object-cover'"
          data-testid="proof-image"
          @error="failed = true"
        >
      </button>

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

      <ProofSourceBadge
        :source="proof.source"
        class="absolute start-2.5 bottom-2.5"
      />
    </div>

    <div class="flex flex-col gap-2.5 px-3.5 pt-3 pb-3.5">
      <!-- The owner's, counted: what would convince next time (#111). -->
      <p
        v-if="proof.votes.doubtReasons.length > 0"
        class="text-[12px] font-semibold text-(--ui-text-muted)"
        data-testid="proof-reasons"
      >
        <span class="font-bold">{{ t.proof.reasonsHeading }}:</span> {{ reasonsText }}
      </p>

      <p
        class="text-[12px] font-semibold text-(--ui-text-muted)"
        data-testid="proof-tally"
      >
        <span data-q2-private>{{
          proof.votes.confirmCount > 0 ? t.proof.confirmedBy(confirmedNames) : t.proof.confirmedByNobody
        }}</span>
        <template v-if="proof.votes.doubtCount > 0">
          · {{ t.proof.doubtCount(proof.votes.doubtCount) }}
        </template>
      </p>

      <template v-if="proof.votes.canIVote && !proof.votes.myVote">
        <div class="flex flex-col gap-2">
          <UButton
            block
            size="xl"
            icon="i-lucide-check"
            :label="t.vote.confirm"
            :disabled="busy"
            data-testid="proof-confirm"
            @click="vote('Confirm')"
          />

          <UButton
            v-if="!choosingReason"
            block
            size="lg"
            color="neutral"
            variant="outline"
            icon="i-lucide-circle-help"
            :label="t.vote.doubt"
            :disabled="busy"
            data-testid="proof-doubt"
            @click="choosingReason = true"
          />

          <div
            v-else
            class="flex flex-col gap-2"
            data-testid="proof-doubt-reasons"
          >
            <p class="text-[12px] font-bold text-(--ui-text-muted)">
              {{ t.proof.reasonPrompt }}
            </p>
            <div class="flex flex-wrap gap-1.5">
              <UButton
                v-for="reason in doubtReasons"
                :key="reason"
                class="min-h-11"
                size="sm"
                color="neutral"
                variant="outline"
                :label="t.proof.doubtReason[reason]"
                :disabled="busy"
                :data-testid="`proof-doubt-${reason}`"
                @click="vote('Doubt', reason)"
              />
              <UButton
                class="min-h-11"
                size="sm"
                color="neutral"
                variant="ghost"
                :label="t.proof.reasonNone"
                :disabled="busy"
                data-testid="proof-doubt-none"
                @click="vote('Doubt')"
              />
            </div>
          </div>
        </div>

        <p class="text-center text-[11px] font-semibold text-(--ui-text-dimmed)">
          {{ t.proof.doubtAnonymous }}
        </p>

        <!--
          A lone checker is told what their doubt can do (#97): it is counted
          and shown, but refusing takes two, and "Anzweifeln" must not look
          like the last word when it is not.
        -->
        <p
          v-if="!proof.votes.doubtCanRefuse"
          class="text-center text-[11px] font-semibold text-(--ui-text-dimmed)"
          data-testid="proof-doubt-alone"
        >
          {{ t.proof.doubtAlone }}
        </p>
      </template>

      <div
        v-else-if="proof.votes.canIVote && proof.votes.myVote"
        class="flex flex-col gap-2"
        data-testid="proof-change-vote"
      >
        <div class="flex items-center justify-between gap-3">
          <p
            class="min-w-0 text-[12px] font-bold text-(--ui-text-muted)"
            role="status"
            data-testid="proof-my-vote"
          >
            {{ proof.votes.myVote === 'Confirm' ? t.proof.votedConfirm : t.proof.votedDoubt }}
          </p>

          <UButton
            class="shrink-0"
            size="sm"
            color="neutral"
            variant="outline"
            :icon="otherVote === 'Confirm' ? 'i-lucide-check' : 'i-lucide-circle-help'"
            :label="otherVote === 'Confirm' ? t.proof.changeToConfirm : t.proof.changeToDoubt"
            :disabled="busy"
            data-testid="proof-change"
            @click="vote(otherVote)"
          />
        </div>

        <p class="text-[11px] font-semibold text-(--ui-text-dimmed)">
          {{ otherVote === 'Doubt' ? `${t.proof.changeHint} ${t.proof.doubtAnonymous}` : t.proof.changeHint }}
        </p>

        <!-- A doubt already cast can say, or change, what was missing (#111). -->
        <div
          v-if="proof.votes.myVote === 'Doubt'"
          class="flex flex-col gap-1.5"
          data-testid="proof-my-reason"
        >
          <p class="text-[11px] font-bold text-(--ui-text-muted)">
            {{ t.proof.reasonPrompt }}
          </p>
          <div class="flex flex-wrap gap-1.5">
            <UButton
              v-for="reason in doubtReasons"
              :key="reason"
              class="min-h-11"
              size="sm"
              color="neutral"
              :variant="proof.votes.myDoubtReason === reason ? 'solid' : 'outline'"
              :aria-pressed="proof.votes.myDoubtReason === reason"
              :label="t.proof.doubtReason[reason]"
              :disabled="busy || proof.votes.myDoubtReason === reason"
              :data-testid="`proof-reason-${reason}`"
              @click="vote('Doubt', reason)"
            />
          </div>
        </div>
      </div>

      <p
        v-else-if="proof.votes.myVote && proof.status === 'Voting'"
        class="text-[12px] font-bold text-(--ui-text-muted)"
        role="status"
        data-testid="proof-my-vote"
      >
        {{ proof.votes.myVote === 'Confirm' ? t.proof.votedConfirm : t.proof.votedDoubt }}
      </p>

      <p
        v-else
        class="text-[12px] font-semibold text-(--ui-text-dimmed)"
        data-testid="proof-status"
      >
        {{ proof.status === 'Voting' ? t.proof.ownProof : proof.status === 'Confirmed' ? t.proof.confirmed : t.proof.rejected }}
      </p>
    </div>
  </article>
</template>
