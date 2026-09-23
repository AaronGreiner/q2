<script setup lang="ts">
import { imageUrl } from '~/api/images'
import type { OwnProof } from '~/api/types'

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
 */
const props = defineProps<{ proof: OwnProof }>()

const emit = defineEmits<{ open: [] }>()

const t = useMessages()
const { public: config } = useRuntimeConfig()

const failed = ref(false)

watch(() => props.proof.imageId, () => {
  failed.value = false
})

const source = computed(() => (failed.value ? null : imageUrl(config.apiBaseUrl, props.proof.imageId)))

const day = computed(() => formatInstantDate(props.proof.createdAt))
const status = computed(() => proofStatusLabel(props.proof.status, t.value))
</script>

<template>
  <button
    type="button"
    class="relative block aspect-square w-full overflow-hidden rounded-(--q2-radius-md) border border-(--ui-border) bg-(--ui-bg-elevated) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
    :aria-label="t.proofGallery.open([proof.goalTitle, day, status].join(', '))"
    :data-status="proof.status"
    data-testid="proof-gallery-tile"
    data-q2-block
    @click="emit('open')"
  >
    <img
      v-if="source"
      :src="source"
      alt=""
      crossorigin="use-credentials"
      decoding="async"
      loading="lazy"
      class="size-full object-cover"
      @error="failed = true"
    >

    <span
      v-else
      class="flex size-full items-center justify-center"
    >
      <UIcon
        name="i-lucide-image-off"
        class="size-6 text-(--ui-text-dimmed)"
        aria-hidden="true"
      />
    </span>

    <span
      v-if="proof.status !== 'Confirmed'"
      class="absolute start-1.5 top-1.5 rounded-full bg-black/60 px-2 py-0.5 text-[10px] font-extrabold text-white"
      aria-hidden="true"
      data-testid="proof-gallery-status"
    >{{ status }}</span>

    <!-- A drop shadow rather than a bar, as on the challenge archive: the
         picture stays whole and the date still reads over it. -->
    <span
      class="absolute start-1.5 bottom-1 text-[10px] font-extrabold text-white [text-shadow:0_1px_3px_rgb(0_0_0/0.9)]"
      aria-hidden="true"
    >{{ day }}</span>
  </button>
</template>
