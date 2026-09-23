<script setup lang="ts">
import type { OwnProof } from '~/api/types'

/**
 * Every photograph you have delivered, by month.
 *
 * Yours only, and all of them: the ones still being voted on and the ones that
 * were not accepted are here too, labelled. Nobody else reads this screen, so
 * leaving the refused ones out would only make your own record less true.
 * Somebody else's profile has no such gallery — a photograph's audience is its
 * goal's, and a collection across goals would widen it.
 */
definePageMeta({ layout: 'plain' })

const t = useMessages()

const { proofs, error, isLoading, refresh } = useOwnProofs()

const months = computed(() => groupByMonth(proofs.value, t.value))

const viewing = ref(false)
const viewed = ref<OwnProof | null>(null)

function view(proof: OwnProof) {
  viewed.value = proof
  viewing.value = true
}

useHead({ title: () => t.value.proofGallery.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader
      :title="t.proofGallery.heading"
      :eyebrow="t.proofGallery.subtitle"
      back-to="/profile"
      :back-label="t.common.back"
    />

    <AppContentPanel>
      <div
        v-if="isLoading"
        class="grid grid-cols-3 gap-1.5"
        aria-busy="true"
        aria-live="polite"
      >
        <span class="sr-only">{{ t.common.loading }}</span>
        <USkeleton
          v-for="tile in 9"
          :key="tile"
          class="aspect-square w-full rounded-(--q2-radius-md)"
        />
      </div>

      <AppErrorState
        v-else-if="error"
        :error="error"
        retryable
        @retry="refresh()"
      />

      <template v-else-if="proofs.length > 0">
        <p class="mb-3 px-0.5 text-[12px] font-semibold text-(--ui-text-dimmed)">
          {{ t.proofGallery.count(proofs.length) }}
        </p>

        <div
          class="flex flex-col gap-5"
          data-testid="proof-gallery"
        >
          <section
            v-for="month in months"
            :key="month.key"
            :aria-labelledby="`proofs-${month.key}`"
          >
            <h2
              :id="`proofs-${month.key}`"
              class="mb-2 px-0.5 text-sm font-extrabold"
            >
              {{ month.label }}
            </h2>

            <div class="grid grid-cols-3 gap-1.5">
              <ProofGalleryTile
                v-for="proof in month.items"
                :key="proof.id"
                :proof="proof"
                @open="view(proof)"
              />
            </div>
          </section>
        </div>
      </template>

      <AppStateMessage
        v-else
        icon="i-lucide-camera"
        :title="t.proofGallery.empty"
        :description="t.proofGallery.emptyHint"
        data-testid="proof-gallery-empty"
      />
    </AppContentPanel>

    <ProofPhotoViewer
      v-model:open="viewing"
      :proof="viewed"
    />
  </div>
</template>
