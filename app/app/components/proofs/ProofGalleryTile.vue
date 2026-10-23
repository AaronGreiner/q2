<script setup lang="ts">
import type { OwnProof } from '~/api/types'
import { proofMediaOf } from '~/utils/proofMedia'

/**
 * One of your own photographs, cropped square, in your profile's gallery.
 *
 * A believed photograph carries nothing but its date. One still being voted on,
 * or one that was not accepted, says so in a grey pill: an outcome is a state,
 * and a state is never the accent or red (ADR 0015) — nothing here is something
 * you can still act on.
 *
 * The goal is not written on the tile; in a three-column grid it would not fit.
 * It is in the button's accessible name and on the full-size view instead.
 *
 * Live or screenshot is written on it, like on every proof (#61), and a
 * screenshot is fitted rather than cropped: a square cut out of a tall picture
 * full of text is the part that says nothing. A clip shows its still with a
 * play mark, and the front camera's picture sits small in its corner (#124,
 * #125).
 */
const props = defineProps<{ proof: OwnProof }>()

const emit = defineEmits<{ open: [] }>()

const t = useMessages()
const media = computed(() => proofMediaOf(props.proof))

const day = computed(() => formatInstantDate(props.proof.createdAt))
const status = computed(() => proofStatusLabel(props.proof.status, t.value))
</script>

<template>
  <button
    type="button"
    class="relative block aspect-square w-full overflow-hidden rounded-(--q2-radius-md) border border-(--ui-border) bg-(--ui-bg-elevated) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    :aria-label="t.proofGallery.open([proof.goalTitle, day, status, t.proof.source[proof.source]].join(', '))"
    :data-status="proof.status"
    data-testid="proof-gallery-tile"
    data-q2-block
    @click="emit('open')"
  >
    <!-- A clip shows its still and a play mark; it plays once opened. -->
    <ProofMedia
      :parts="media"
      :fit="proof.source === 'Screenshot' ? 'contain' : 'cover'"
      mode="still"
      inset-corner="bottom-end"
    />

    <span
      v-if="proof.status !== 'Confirmed'"
      class="absolute start-1.5 top-1.5 rounded-full bg-black/60 px-2 py-0.5 text-[10px] font-extrabold text-white"
      aria-hidden="true"
      data-testid="proof-gallery-status"
    >{{ status }}</span>

    <!-- Stacked rather than side by side: in a three-column grid on a phone
         "Screenshot" and a date do not fit on one line. A drop shadow rather
         than a bar for the date, as on the challenge archive: the picture
         stays whole and the date still reads over it. -->
    <span
      class="absolute start-1.5 bottom-1 flex flex-col items-start gap-0.5"
      aria-hidden="true"
    >
      <ProofSourceBadge :source="proof.source" />
      <span class="text-[10px] font-extrabold text-white [text-shadow:0_1px_3px_rgb(0_0_0/0.9)]">{{ day }}</span>
    </span>
  </button>
</template>
