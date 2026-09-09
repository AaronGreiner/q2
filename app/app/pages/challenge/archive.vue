<script setup lang="ts">
/**
 * Your own challenge archive.
 *
 * Only what you took part in. Challenges you sat out do not appear at all, and
 * other people's contributions are deliberately not kept: the room is transient
 * so that nobody builds a lasting collection of other people's pictures, and
 * whoever could keep them could also pass them on. Your own memory is untouched
 * by that.
 *
 * The prompt travels with every picture, because it has to. A photograph of a
 * desk says nothing in six months; "Zeig deinen Arbeitsplatz" does.
 */
definePageMeta({ layout: 'plain' })

const t = useMessages()

const { entries, error, isLoading, refresh } = useChallengeArchive()

useHead({ title: () => t.value.challenge.archive })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader
      :title="t.challenge.archive"
      :eyebrow="t.challenge.archiveSubtitle"
      back-to="/challenge"
      :back-label="t.common.back"
    />

    <div class="q2-scroll flex-1 px-[18px] pt-1.5 pb-6">
      <div
        v-if="isLoading"
        class="grid grid-cols-3 gap-1.5"
        aria-busy="true"
        aria-live="polite"
      >
        <span class="sr-only">{{ t.common.loading }}</span>
        <USkeleton
          v-for="tile in 6"
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

      <template v-else-if="entries.length > 0">
        <p class="mb-3 px-0.5 text-[12px] font-semibold text-(--ui-text-dimmed)">
          {{ t.challenge.archiveCount(entries.length) }}
        </p>

        <div
          class="grid grid-cols-3 gap-1.5"
          data-testid="challenge-archive"
        >
          <ChallengeArchiveTile
            v-for="item in entries"
            :key="item.entry.id"
            :item="item"
          />
        </div>
      </template>

      <AppStateMessage
        v-else
        icon="i-lucide-package-open"
        :title="t.challenge.archiveEmpty"
        :description="t.challenge.archiveEmptyHint"
        data-testid="challenge-archive-empty"
      />
    </div>
  </div>
</template>
