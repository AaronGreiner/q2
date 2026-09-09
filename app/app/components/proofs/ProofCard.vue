<script setup lang="ts">
import { imageUrl } from '~/api/images'
import type { Proof, ProofVoteValue } from '~/api/types'

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
 * - **"Live aufgenommen" is stated.** A picture chosen from a gallery can be
 *   any age, and the person voting is entitled to know which they are looking
 *   at. It is a label, never a refusal — a phone whose camera permission was
 *   denied still has to be able to deliver.
 */
const props = withDefaults(defineProps<{
  proof: Proof
  /** What was promised, so the picture can be judged against something. */
  goalTitle?: string | null
  busy?: boolean
}>(), {
  goalTitle: null,
  busy: false,
})

const emit = defineEmits<{ vote: [value: ProofVoteValue] }>()

const t = useMessages()
const now = useNow()
const { public: config } = useRuntimeConfig()

const failed = ref(false)

watch(() => props.proof.imageId, () => {
  failed.value = false
})

const source = computed(() => (failed.value ? null : imageUrl(config.apiBaseUrl, props.proof.imageId)))

/** Whole hours left, floored — "noch 2 Stunden" must never read as more than there is. */
const hoursLeft = computed(() => {
  // `useNow` is epoch millis, shared across the app so the server and the
  // browser agree on what "now" was at hydration.
  const remaining = new Date(props.proof.expiresAt).getTime() - now.value

  return remaining <= 0 ? null : Math.floor(remaining / 3_600_000)
})

const confirmedNames = computed(() =>
  props.proof.votes.confirmedBy.map(person => person.displayName).join(', '))
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

      <span
        v-if="hoursLeft !== null"
        class="shrink-0 text-[11px] font-bold text-(--ui-text-dimmed)"
        data-testid="proof-expiry"
      >{{ t.proof.expiresIn(hoursLeft) }}</span>
      <span
        v-else
        class="shrink-0 text-[11px] font-bold text-(--ui-text-dimmed)"
      >{{ t.proof.expired }}</span>
    </header>

    <!--
      A square frame at a fixed ratio, so the card is the same height before and
      after the picture arrives. A feed that reflows as each photograph loads is
      a feed somebody votes on by accident.
    -->
    <div class="relative aspect-square w-full bg-(--ui-bg-elevated)">
      <img
        v-if="source"
        :src="source"
        alt=""
        crossorigin="use-credentials"
        decoding="async"
        class="size-full object-cover"
        data-testid="proof-image"
        @error="failed = true"
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
      </div>

      <span
        class="absolute start-2.5 bottom-2.5 inline-flex items-center gap-1 rounded-full bg-black/60 px-2 py-1 text-[10px] font-bold text-white backdrop-blur-sm"
        data-testid="proof-provenance"
      >
        <UIcon
          :name="proof.capturedInApp ? 'i-lucide-camera' : 'i-lucide-image'"
          class="size-3"
          aria-hidden="true"
        />
        {{ proof.capturedInApp ? t.proof.capturedLive : t.proof.fromLibrary }}
      </span>
    </div>

    <div class="flex flex-col gap-2.5 px-3.5 pt-3 pb-3.5">
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

      <template v-if="proof.votes.canIVote">
        <div class="flex flex-col gap-2">
          <UButton
            block
            size="xl"
            icon="i-lucide-check"
            :label="t.vote.confirm"
            :disabled="busy"
            data-testid="proof-confirm"
            @click="emit('vote', 'Confirm')"
          />

          <UButton
            block
            size="lg"
            color="neutral"
            variant="outline"
            icon="i-lucide-circle-help"
            :label="t.vote.doubt"
            :disabled="busy"
            data-testid="proof-doubt"
            @click="emit('vote', 'Doubt')"
          />
        </div>

        <p class="text-center text-[11px] font-semibold text-(--ui-text-dimmed)">
          {{ t.proof.doubtAnonymous }}
        </p>
      </template>

      <p
        v-else-if="proof.votes.myVote"
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
