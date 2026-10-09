<script setup lang="ts">
import type { ProofSource } from '~/api/types'
import { proofSourceIcons } from '~/utils/proofSource'

/**
 * Which way a proof came in: "Live", "Screenshot", or — for proofs from before
 * it was recorded — "Ohne Angabe".
 *
 * Shown on every proof, wherever it appears (#61): friends deciding whether a
 * window counted should always know what they are looking at. A word and an
 * icon, never colour: it is a state, not something to do (ADR 0015), and the
 * same grey-on-black pill sits on top of any picture.
 *
 * It says which route the app used, not that the picture is fresh — the
 * server cannot know that, and the wording does not claim it.
 */
defineProps<{ source: ProofSource }>()

const t = useMessages()
</script>

<template>
  <span
    class="inline-flex items-center gap-1 rounded-full bg-black/60 px-2 py-1 text-[10px] leading-none font-bold text-white backdrop-blur-sm"
    :data-source="source"
    data-testid="proof-source"
  >
    <UIcon
      :name="proofSourceIcons[source]"
      class="size-3 shrink-0"
      aria-hidden="true"
    />
    {{ t.proof.source[source] }}
  </span>
</template>
