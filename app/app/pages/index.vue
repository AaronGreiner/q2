<script setup lang="ts">
import type { Image } from '~/api/types'

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

const { profile, due, goals, feed, error, isLoading, refresh, toggleKudos } = useHome()
const { isDelivering, deliver, maxEdge } = useProofDelivery()
const { proofs: waiting, refresh: refreshWaiting } = usePendingProofs()

// Its own read rather than part of `useHome`: the challenge is one row and the
// dashboard is four, and a shared key would refetch all of them whenever
// somebody joins in from the room and comes back.
const { room: challenge } = useChallengeRoom()

/*
 * Which goal the camera is open for.
 *
 * Held on the screen rather than in the row: `PhotoCapture` is a sheet, and a
 * sheet per row would be one drawer per goal — with all the stacking that
 * deadlocked the profile screen when it tried exactly that.
 */
const deliveringFor = ref<string | null>(null)

async function onDelivered(image: Image) {
  const goalId = deliveringFor.value
  deliveringFor.value = null

  if (goalId && await deliver(goalId, image)) {
    await Promise.all([refresh(), refreshWaiting()])
  }
}

// Only the first few: the whole list is one tap away under "Alle anzeigen",
// and a start screen that shows everything is not a start screen.
const nextDue = computed(() => due.value.slice(0, 3))

const greeting = computed(() => t.value.home.greeting(new Date(now.value).getUTCHours()))

useHead({ title: () => t.value.nav.home })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader
      :eyebrow="greeting"
      :title="profile ? profile.person.displayName : t.app.name"
      :private-title="Boolean(profile)"
    >
      <template #actions>
        <!--
          The bell, and it is a plain link rather than a badge count: a number
          on it would turn "have my friends done anything" into something to
          clear, which is the mechanic this product is trying not to be.
        -->
        <UButton
          to="/activity"
          icon="i-lucide-bell"
          color="neutral"
          variant="outline"
          size="lg"
          :ui="{ base: 'size-11 justify-center rounded-full' }"
          :aria-label="t.activityOverview.open"
          data-testid="open-activity"
        />

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

        <!--
          The offer with nothing at stake, and it sits below the streak and
          above everything with a deadline. It is not urgent and must not look
          it; it is also the one thing on this screen that is simply nice, so
          it comes before the list of what is owed.

          Absent entirely on a day with no challenge running — a banner reading
          "nothing today" is furniture.
        -->
        <ChallengeBanner
          v-if="challenge"
          class="mt-3"
          :room="challenge"
        />

        <!--
          Your own windows that are about to go, above everything else on the
          screen. They are the only thing here with a deadline tonight.

          Absent for most of the day: the rule only turns on in the evening, so
          `atRisk` is empty and this section is not drawn at all rather than
          being an empty heading.
        -->
        <section
          v-if="profile.atRisk.length > 0"
          class="mt-3"
          aria-labelledby="risk-heading"
        >
          <h2
            id="risk-heading"
            class="mb-2 px-0.5 text-[11px] font-bold tracking-wide text-(--ui-text-muted) uppercase"
          >
            {{ t.risk.heading }}
          </h2>

          <div class="flex flex-col gap-2">
            <RiskCard
              v-for="goal in profile.atRisk"
              :key="goal.id"
              :goal="goal"
              :busy="isDelivering"
              @deliver="deliveringFor = $event"
            />
          </div>
        </section>

        <!--
          The one thing on this screen that is somebody else's business.
          It sits above your own goals on purpose: a friend's photograph has a
          deadline on it, and yours does not expire in twelve hours.

          The accent is spent here because it is a thing to do right now, and it
          disappears entirely when there is nothing waiting — a permanent banner
          reading "0" is furniture.
        -->
        <NuxtLink
          v-if="waiting.length > 0"
          to="/vote"
          class="mt-3 flex items-center gap-3 rounded-(--q2-radius-lg) bg-(--q2-accent-solid) px-3.5 py-3 text-(--q2-accent-contrast) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
          data-testid="vote-banner"
        >
          <UIcon
            name="i-lucide-gavel"
            class="size-5 shrink-0"
            aria-hidden="true"
          />
          <span class="min-w-0 flex-1 text-[13px] font-extrabold">{{ t.vote.banner(waiting.length) }}</span>
          <span class="shrink-0 text-[12px] font-bold underline">{{ t.vote.open }}</span>
        </NuxtLink>

        <section
          class="mt-6"
          aria-labelledby="today-heading"
        >
          <div class="mb-3 flex items-center justify-between px-0.5">
            <h2
              id="today-heading"
              class="q2-eyebrow"
            >
              {{ t.home.todayHeading }}
            </h2>
            <NuxtLink
              to="/goals"
              class="-my-3 min-w-11 py-3 text-center text-[13px] font-bold text-(--ui-text-muted) hover:underline"
            >
              {{ t.common.showAll }}
            </NuxtLink>
          </div>

          <div
            v-if="nextDue.length > 0"
            class="flex flex-col gap-2.5"
          >
            <GoalWindowRow
              v-for="goal in nextDue"
              :key="goal.id"
              :goal="goal"
              :busy="isDelivering"
              @deliver="deliveringFor = $event"
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
              class="q2-eyebrow"
            >
              {{ t.home.goalsHeading }}
            </h2>
            <NuxtLink
              to="/goals"
              class="-my-3 min-w-11 py-3 text-center text-[13px] font-bold text-(--ui-text-muted) hover:underline"
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
            class="q2-eyebrow mb-3 px-0.5"
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
      </template>
    </div>

    <!--
      One camera for the whole screen, opened by whichever row asked for it. A
      sheet per row would be one drawer per goal, and two open drawers deadlock
      — see PhotoCapture.
    -->
    <PhotoCapture
      :open="deliveringFor !== null"
      purpose="Proof"
      :max-edge="maxEdge"
      @update:open="value => { if (!value) deliveringFor = null }"
      @uploaded="onDelivered"
    />
  </div>
</template>
