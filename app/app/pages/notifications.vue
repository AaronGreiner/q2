<script setup lang="ts">
/**
 * The bell: what happened to you.
 *
 * Only what has no other home is here. A message lives in its chat, a request
 * on the search screen, a vote on the start screen's banner — each with a
 * count of its own — so nothing on this screen is a second copy of something
 * that already asks to be cleared somewhere else
 * (docs/adr/0024-one-notification-pipeline.md).
 *
 * What is new sits under its own heading above the rest of the last thirty
 * days. Opening the screen is what clears the badge.
 */
const t = useMessages()
const now = useNow()

const { fresh, earlier, isEmpty, error, isLoading, refresh } = useNotifications()

useHead({ title: () => t.value.notify.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader
      :title="t.notify.heading"
      back-to="/"
      :back-label="t.common.back"
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

      <AppStateMessage
        v-else-if="isEmpty"
        icon="i-lucide-bell"
        :title="t.notify.empty"
        :description="t.notify.emptyHint"
        data-testid="notifications-empty"
      />

      <template v-else>
        <section
          v-if="fresh.length > 0"
          aria-labelledby="fresh-heading"
        >
          <h2
            id="fresh-heading"
            class="mb-2 px-0.5 text-[11px] font-bold tracking-wide text-(--ui-text-muted) uppercase"
          >
            {{ t.notify.fresh }}
          </h2>

          <ul
            class="flex list-none flex-col gap-2.5 p-0"
            data-testid="notifications-fresh"
          >
            <li
              v-for="line in fresh"
              :key="line.id ?? line.occurredAt"
            >
              <NotificationRow
                :line="line"
                :now="now"
              />
            </li>
          </ul>
        </section>

        <section
          v-if="earlier.length > 0"
          :class="fresh.length > 0 ? 'mt-6' : ''"
          aria-labelledby="earlier-heading"
        >
          <h2
            id="earlier-heading"
            class="mb-2 px-0.5 text-[11px] font-bold tracking-wide text-(--ui-text-muted) uppercase"
          >
            {{ t.notify.earlier }}
          </h2>

          <ul
            class="flex list-none flex-col gap-2.5 p-0"
            data-testid="notifications-earlier"
          >
            <li
              v-for="line in earlier"
              :key="line.id ?? line.occurredAt"
            >
              <NotificationRow
                :line="line"
                :now="now"
              />
            </li>
          </ul>
        </section>
      </template>
    </div>
  </div>
</template>
