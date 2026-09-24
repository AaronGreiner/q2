<script setup lang="ts">
/**
 * Everything your friends have been up to, in one place.
 *
 * The start screen shows the newest few, with a link here under them; this is
 * the whole thing.
 *
 * It used to open with a second kind of entry — a warning that somebody was
 * about to miss — kept apart from the good news above it. That warning is a
 * line in each friend's bell now, addressed to the people it is for
 * (docs/adr/0024-one-notification-pipeline.md), which leaves this what it was
 * always meant to be: what friends did.
 */
const t = useMessages()
const now = useNow()

const { feed, error, isLoading, refresh, toggleKudos } = useActivityOverview()

useHead({ title: () => t.value.activityOverview.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader
      :title="t.activityOverview.heading"
      back-to="/"
      :back-label="t.common.back"
    />

    <AppContentPanel>
      <div
        v-if="isLoading"
        class="flex flex-col gap-2.5"
        aria-busy="true"
        aria-live="polite"
      >
        <span class="sr-only">{{ t.common.loading }}</span>
        <USkeleton
          v-for="row in 4"
          :key="row"
          class="h-16 w-full rounded-(--q2-radius-lg)"
        />
      </div>

      <AppErrorState
        v-else-if="error"
        :error="error"
        retryable
        @retry="refresh()"
      />

      <section
        v-else-if="feed.length > 0"
        aria-labelledby="feed-heading"
      >
        <h2
          id="feed-heading"
          class="mb-2 px-0.5 text-[11px] font-bold tracking-wide text-(--ui-text-muted) uppercase"
        >
          {{ t.home.feedHeading }}
        </h2>

        <div
          class="flex flex-col gap-2.5"
          data-testid="activity-feed"
        >
          <ActivityRow
            v-for="entry in feed"
            :key="entry.id"
            :activity="entry"
            :now="now"
            @kudos="toggleKudos"
          />
        </div>
      </section>

      <AppStateMessage
        v-else
        icon="i-lucide-users"
        :title="t.activityOverview.empty"
        :description="t.activityOverview.emptyHint"
        data-testid="activity-empty"
      />
    </AppContentPanel>
  </div>
</template>
