<script setup lang="ts">
/**
 * Your own profile: who you are, how you are doing, and what you have earned.
 *
 * Shares the `profile` async-data key with the layout, so the badges in the tab
 * bar and this screen come from one request.
 */
const t = useMessages()
const now = useNow()

const { profile, error, isLoading, refresh } = useProfile()

useHead({ title: () => t.value.profile.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader :title="t.profile.heading">
      <template #actions>
        <UButton
          to="/settings"
          icon="i-lucide-settings"
          color="neutral"
          variant="outline"
          size="lg"
          :ui="{ base: 'size-11 justify-center rounded-full' }"
          :aria-label="t.profile.openSettings"
          data-testid="open-settings"
        />
      </template>
    </AppScreenHeader>

    <div class="q2-scroll flex-1 px-[18px] pt-1.5 pb-6">
      <div
        v-if="isLoading"
        class="flex flex-col items-center gap-4"
        aria-busy="true"
        aria-live="polite"
      >
        <span class="sr-only">{{ t.common.loading }}</span>
        <USkeleton class="size-22 rounded-full" />
        <USkeleton class="h-6 w-40" />
        <USkeleton class="h-20 w-full rounded-(--q2-radius-lg)" />
      </div>

      <AppErrorState
        v-else-if="error"
        :error="error"
        retryable
        @retry="refresh()"
      />

      <template v-else-if="profile">
        <section
          class="flex flex-col items-center py-1.5 text-center"
          aria-labelledby="profile-name"
        >
          <span
            class="flex size-22 items-center justify-center rounded-full text-[32px] font-extrabold text-white"
            :style="{ background: profile.person.avatarColor }"
            aria-hidden="true"
          >{{ profile.person.initials }}</span>

          <h2
            id="profile-name"
            class="mt-3 text-[21px] font-extrabold"
          >
            {{ profile.person.displayName }}
          </h2>
          <p class="text-[13px] font-semibold text-(--ui-text-muted)">
            {{ profile.person.handle }}
          </p>

          <p class="mt-2.5 flex items-center gap-1.5 rounded-full bg-(--q2-amber-soft) px-3 py-1.5 text-xs font-extrabold text-(--q2-amber)">
            <UIcon
              name="i-lucide-flame"
              class="size-3.5"
              aria-hidden="true"
            />
            {{ t.profile.streakBadge(profile.streak) }}
          </p>
        </section>

        <dl class="mt-4 flex list-none gap-2.5">
          <div class="q2-card flex-1 px-2 py-3.5 text-center">
            <dd class="text-[22px] font-extrabold">
              {{ profile.streak }}
            </dd>
            <dt class="mt-0.5 text-[11px] font-bold text-(--ui-text-muted)">
              {{ t.profile.streak }}
            </dt>
          </div>
          <div class="q2-card flex-1 px-2 py-3.5 text-center">
            <dd class="text-[22px] font-extrabold text-(--ui-primary)">
              {{ profile.kudosReceived }}
            </dd>
            <dt class="mt-0.5 text-[11px] font-bold text-(--ui-text-muted)">
              {{ t.profile.kudos }}
            </dt>
          </div>
          <div class="q2-card flex-1 px-2 py-3.5 text-center">
            <dd class="text-[22px] font-extrabold">
              {{ profile.goalsCompleted }}
            </dd>
            <dt class="mt-0.5 text-[11px] font-bold text-(--ui-text-muted)">
              {{ t.profile.goals }}
            </dt>
          </div>
        </dl>

        <section
          class="mt-6"
          aria-labelledby="badges-heading"
        >
          <h2
            id="badges-heading"
            class="mb-3 px-0.5 text-base font-extrabold"
          >
            {{ t.profile.badges }}
          </h2>

          <BadgeGrid :badges="profile.badges" />
        </section>

        <section
          class="mt-6"
          aria-labelledby="activity-heading"
        >
          <h2
            id="activity-heading"
            class="mb-3 px-0.5 text-base font-extrabold"
          >
            {{ t.profile.activity }}
          </h2>

          <div
            v-if="profile.recentActivity.length > 0"
            class="flex flex-col gap-2.5"
            data-testid="own-activity"
          >
            <ActivityRow
              v-for="entry in profile.recentActivity"
              :key="entry.id"
              :activity="entry"
              :now="now"
              readonly
            />
          </div>

          <AppStateMessage
            v-else
            icon="i-lucide-sparkles"
            :title="t.profile.noActivity"
            :description="t.profile.noActivityHint"
          />
        </section>
      </template>
    </div>
  </div>
</template>
