<script setup lang="ts">
/**
 * The photographs waiting for your verdict, one at a time.
 *
 * One card rather than a list, and that is the whole design of the screen. A
 * scrolling column of photographs invites tapping "confirm" down the side
 * without looking, which is exactly the inattention the vote exists to prevent
 * — and it would make the count at the bottom meaningless. One card at a time
 * costs a decision each.
 *
 * There is no "skip". A photograph you passed over would either come back
 * (and the queue never empties) or would not (and you silently abstained on a
 * friend). Leaving the screen is the way to not decide, and it needs no button.
 *
 * The start screen shows the same stack; this screen is where a notification
 * about a waiting photograph lands, and it has the whole height for it.
 */
const t = useMessages()

const { proofs, error, isLoading, isVoting, refresh, vote } = usePendingProofs()

useHead({ title: () => t.value.vote.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader :title="t.vote.waitingHeading">
      <template #actions>
        <span
          v-if="proofs.length > 0"
          class="rounded-full bg-(--ui-bg-accented) px-3 py-1.5 text-[12px] font-extrabold text-(--ui-text-toned)"
          data-testid="vote-remaining"
        >{{ t.vote.remaining(proofs.length) }}</span>
      </template>
    </AppScreenHeader>

    <div class="q2-scroll flex-1 overflow-x-hidden px-[18px] pt-1.5 pb-6">
      <div
        v-if="isLoading"
        class="flex flex-col gap-3"
        aria-busy="true"
        aria-live="polite"
      >
        <span class="sr-only">{{ t.common.loading }}</span>
        <USkeleton class="h-14 w-full rounded-(--q2-radius-lg)" />
        <USkeleton class="aspect-square w-full rounded-(--q2-radius-lg)" />
      </div>

      <AppErrorState
        v-else-if="error"
        :error="error"
        retryable
        @retry="refresh()"
      />

      <template v-else-if="proofs.length > 0">
        <h2 class="mb-3 px-0.5 text-base font-extrabold">
          {{ t.vote.heading }}
        </h2>

        <ProofSwipeStack
          :cards="proofs"
          :busy="isVoting"
          @vote="vote"
        />
      </template>

      <AppStateMessage
        v-else
        icon="i-lucide-circle-check"
        :title="t.vote.empty"
        :description="t.vote.emptyHint"
        data-testid="vote-empty"
      />
    </div>
  </div>
</template>
