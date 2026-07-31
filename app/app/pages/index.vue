<script setup lang="ts">
/**
 * The start screen.
 *
 * The page composes and owns the wiring — what is loaded, what happens on a
 * tap. Rendering is delegated to components, and every rule about goals,
 * streaks or kudos lives in `useHome` or on the server.
 */
const t = useMessages()
const now = useNow()
const theme = useTheme()

const { profile, tasks, goals, feed, leaderboard, error, isLoading, refresh, toggleTask, toggleKudos } = useHome()

// Only the first few: the whole list is one tap away under "Alle anzeigen",
// and a start screen that shows everything is not a start screen.
const nextTasks = computed(() => tasks.value.slice(0, 3))

const greeting = computed(() => t.value.home.greeting(new Date(now.value).getUTCHours()))

useHead({ title: () => t.value.nav.home })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader
      :eyebrow="greeting"
      :title="profile ? `${profile.person.displayName} 👋` : t.app.name"
    >
      <template #actions>
        <UButton
          :icon="theme.isDark.value ? 'i-lucide-sun' : 'i-lucide-moon'"
          color="neutral"
          variant="outline"
          size="lg"
          :ui="{ base: 'size-11 justify-center rounded-full' }"
          :aria-label="theme.isDark.value ? t.settings.themeLight : t.settings.themeDark"
          data-testid="theme-toggle"
          @click="theme.toggle()"
        />
      </template>
    </AppScreenHeader>

    <div class="q2-scroll flex-1 px-[18px] pt-0.5 pb-6">
      <div
        v-if="isLoading"
        class="flex flex-col gap-4"
        aria-busy="true"
        aria-live="polite"
      >
        <span class="sr-only">{{ t.common.loading }}</span>
        <USkeleton class="h-36 w-full rounded-(--q2-radius-xl)" />
        <USkeleton class="h-16 w-full rounded-(--q2-radius-lg)" />
        <USkeleton class="h-16 w-full rounded-(--q2-radius-lg)" />
      </div>

      <AppErrorState
        v-else-if="error"
        :error="error"
        retryable
        @retry="refresh()"
      />

      <template v-else-if="profile">
        <StreakHero
          :streak="profile.streak"
          :week="profile.weekActivity"
        />

        <TodayProgressCard
          class="mt-3"
          :today="profile.today"
          :streak="profile.streak"
        />

        <section
          class="mt-6"
          aria-labelledby="today-heading"
        >
          <div class="mb-3 flex items-center justify-between px-0.5">
            <h2
              id="today-heading"
              class="text-base font-extrabold"
            >
              {{ t.home.todayHeading }}
            </h2>
            <NuxtLink
              to="/goals"
              class="-my-3 py-3 text-[13px] font-bold text-(--ui-primary) hover:underline"
            >
              {{ t.common.showAll }}
            </NuxtLink>
          </div>

          <div
            v-if="nextTasks.length > 0"
            class="flex flex-col gap-2.5"
          >
            <GoalTaskRow
              v-for="task in nextTasks"
              :key="task.id"
              :task="task"
              @toggle="toggleTask"
            />
          </div>

          <AppStateMessage
            v-else
            icon="i-lucide-circle-check"
            :title="t.home.noTasks"
            :description="t.home.noTasksHint"
            data-testid="home-no-tasks"
          />
        </section>

        <section
          v-if="goals.length > 0"
          class="mt-6"
          aria-labelledby="home-goals-heading"
        >
          <div class="mb-3 flex items-center justify-between px-0.5">
            <h2
              id="home-goals-heading"
              class="text-base font-extrabold"
            >
              {{ t.home.goalsHeading }}
            </h2>
            <NuxtLink
              to="/goals"
              class="-my-3 py-3 text-[13px] font-bold text-(--ui-primary) hover:underline"
            >
              {{ t.common.more }}
            </NuxtLink>
          </div>

          <!-- Bleeds to both edges so the strip reads as scrollable. -->
          <div class="q2-scroll-x -mx-[18px] flex gap-3 px-[18px] pb-1">
            <GoalTile
              v-for="goal in goals"
              :key="goal.id"
              :goal="goal"
            />
          </div>
        </section>

        <section
          class="mt-6"
          aria-labelledby="feed-heading"
        >
          <h2
            id="feed-heading"
            class="mb-3 px-0.5 text-base font-extrabold"
          >
            {{ t.home.feedHeading }}
          </h2>

          <div
            v-if="feed.length > 0"
            class="flex flex-col gap-2.5"
          >
            <ActivityRow
              v-for="entry in feed"
              :key="entry.id"
              :activity="entry"
              :now="now"
              @kudos="toggleKudos"
            />
          </div>

          <AppStateMessage
            v-else
            icon="i-lucide-users"
            :title="t.home.noFeed"
            :description="t.home.noFeedHint"
          />
        </section>

        <section
          v-if="leaderboard.length > 0"
          class="mt-6"
          aria-labelledby="leaderboard-heading"
        >
          <h2
            id="leaderboard-heading"
            class="mb-3 flex items-center gap-2 px-0.5 text-base font-extrabold"
          >
            <UIcon
              name="i-lucide-trophy"
              class="size-[18px] text-(--q2-amber)"
              aria-hidden="true"
            />
            {{ t.home.leaderboardHeading }}
          </h2>

          <LeaderboardCard :entries="leaderboard" />
        </section>
      </template>
    </div>
  </div>
</template>
