<script setup lang="ts">
/**
 * Everything your friends have been up to, in one place.
 *
 * The start screen shows the newest few; this is the whole thing. It exists
 * because stage 5 gave the feed a second kind of entry — a warning that
 * somebody is about to miss — and a warning three taps down a dashboard is a
 * warning nobody acts on.
 *
 * The two kinds are separated rather than interleaved by time. A warning has a
 * deadline tonight and everything else does not, and a list sorted purely by
 * recency would bury the one row that still has something to be done about it
 * under six people's good news.
 */
const t = useMessages()
const now = useNow()

const { feed, error, isLoading, refresh, toggleKudos } = useActivityOverview()

const warnings = computed(() => feed.value.filter(isWarning))
const rest = computed(() => feed.value.filter(entry => !isWarning(entry)))

useHead({ title: () => t.value.activityOverview.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader
      :title="t.activityOverview.heading"
      back-to="/"
    />

    <div class="q2-scroll flex-1 px-[18px] pt-1.5 pb-6">
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

      <template v-else-if="feed.length > 0">
        <section
          v-if="warnings.length > 0"
          aria-labelledby="warnings-heading"
        >
          <h2
            id="warnings-heading"
            class="mb-2 px-0.5 text-[11px] font-bold tracking-wide text-(--ui-text-muted) uppercase"
          >
            {{ t.activityOverview.warnings }}
          </h2>

          <div
            class="flex flex-col gap-2.5"
            data-testid="activity-warnings"
          >
            <ActivityRow
              v-for="entry in warnings"
              :key="entry.id"
              :activity="entry"
              :now="now"
              @kudos="toggleKudos"
            />
          </div>
        </section>

        <section
          v-if="rest.length > 0"
          :class="warnings.length > 0 ? 'mt-6' : ''"
          aria-labelledby="rest-heading"
        >
          <h2
            id="rest-heading"
            class="mb-2 px-0.5 text-[11px] font-bold tracking-wide text-(--ui-text-muted) uppercase"
          >
            {{ warnings.length > 0 ? t.activityOverview.everythingElse : t.home.feedHeading }}
          </h2>

          <div
            class="flex flex-col gap-2.5"
            data-testid="activity-rest"
          >
            <ActivityRow
              v-for="entry in rest"
              :key="entry.id"
              :activity="entry"
              :now="now"
              @kudos="toggleKudos"
            />
          </div>
        </section>
      </template>

      <AppStateMessage
        v-else
        icon="i-lucide-users"
        :title="t.activityOverview.empty"
        :description="t.activityOverview.emptyHint"
        data-testid="activity-empty"
      />
    </div>
  </div>
</template>
