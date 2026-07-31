<script setup lang="ts">
/**
 * One goal: the ring, the team, and the button that moves it.
 *
 * Loaded on the server so the page is meaningful without JavaScript and a
 * shared link has the right title.
 */
definePageMeta({ layout: 'plain' })

const route = useRoute()
const t = useMessages()

const id = computed(() => String(route.params.id))
const { detail, error, isMissing, isLoading, refresh, contribute, isContributing, toggleTask } = useGoalDetail(id)

const goal = computed(() => detail.value?.goal ?? null)
const reminder = computed(() => (goal.value ? formatClock(goal.value.reminderAt) : null))

useHead({ title: () => goal.value?.title ?? t.value.goals.detailHeading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader
      :title="t.goals.detailHeading"
      back-to="/goals"
      :back-label="t.common.back"
    />

    <div class="q2-scroll flex-1 px-[18px] pt-1 pb-8">
      <div
        v-if="isLoading"
        class="flex flex-col gap-4"
        aria-busy="true"
        aria-live="polite"
      >
        <span class="sr-only">{{ t.common.loading }}</span>
        <USkeleton class="h-14 w-2/3" />
        <USkeleton class="h-60 w-full rounded-(--q2-radius-lg)" />
      </div>

      <AppErrorState
        v-else-if="error"
        :error="error"
        retryable
        @retry="refresh()"
      />

      <AppStateMessage
        v-else-if="isMissing || !goal || !detail"
        icon="i-lucide-compass"
        :title="t.goals.notFound"
        :description="t.goals.notFoundHint"
        data-testid="goal-not-found"
      >
        <UButton
          to="/goals"
          icon="i-lucide-target"
        >
          {{ t.goals.heading }}
        </UButton>
      </AppStateMessage>

      <article
        v-else
        data-testid="goal-detail"
      >
        <header class="flex items-center gap-3.5">
          <span
            class="flex size-14 shrink-0 items-center justify-center rounded-(--q2-radius-lg) bg-(--q2-accent-soft) text-(--q2-accent-soft-text)"
            aria-hidden="true"
          >
            <UIcon
              :name="goalIconName(goal.icon)"
              class="size-7"
            />
          </span>

          <div class="min-w-0 flex-1">
            <h1 class="text-xl leading-tight font-extrabold text-pretty">
              {{ goal.title }}
            </h1>
            <p class="mt-0.5 text-[13px] font-semibold text-(--ui-text-muted)">
              {{ t.rhythm[goal.rhythm] }} · {{ goalSubtitle(goal, t) }}
            </p>
          </div>
        </header>

        <p
          v-if="goal.description"
          class="mt-3 text-sm text-(--ui-text-muted)"
        >
          {{ goal.description }}
        </p>

        <section
          class="q2-card mt-4 p-5 text-center"
          aria-labelledby="goal-progress-heading"
        >
          <h2
            id="goal-progress-heading"
            class="sr-only"
          >
            {{ t.goals.progressLabel(goal.progressPercent) }}
          </h2>

          <AppProgressRing
            class="mx-auto"
            :percent="goal.progressPercent"
            :size="130"
            :label="t.goals.progressLabel(goal.progressPercent)"
          >
            <span class="text-[34px] leading-none font-extrabold text-(--ui-primary)">{{ goal.progressPercent }}%</span>
            <span class="mt-0.5 text-[11px] font-bold text-(--ui-text-muted)">{{ t.goals.reached }}</span>
          </AppProgressRing>

          <UButton
            class="mt-4 w-full justify-center"
            size="xl"
            icon="i-lucide-circle-check-big"
            :loading="isContributing"
            :disabled="goal.status !== 'Active'"
            data-testid="goal-contribute"
            @click="contribute()"
          >
            {{ goal.status === 'Active' ? t.goals.contribute : t.goals.contributeDone }}
          </UButton>
        </section>

        <div class="mt-3.5 flex gap-2.5">
          <div class="q2-card flex-1 p-3.5">
            <p class="flex items-center gap-1.5 text-xs font-extrabold text-(--q2-amber)">
              <UIcon
                name="i-lucide-flame"
                class="size-4"
                aria-hidden="true"
              />
              {{ t.goals.streak }}
            </p>
            <p class="mt-1 text-[22px] font-extrabold">
              {{ t.goals.streakDays(goal.streak) }}
            </p>
          </div>

          <div class="q2-card flex-1 p-3.5">
            <p class="flex items-center gap-1.5 text-xs font-extrabold text-(--ui-text-muted)">
              <UIcon
                name="i-lucide-bell"
                class="size-4"
                aria-hidden="true"
              />
              {{ t.goals.reminder }}
            </p>
            <p class="mt-1 text-lg font-extrabold">
              {{ reminder ?? t.goals.noReminder }}
            </p>
          </div>
        </div>

        <section
          v-if="detail.tasks.length > 0"
          class="mt-6"
          aria-labelledby="goal-tasks-heading"
        >
          <h2
            id="goal-tasks-heading"
            class="mb-3 px-0.5 text-base font-extrabold"
          >
            {{ t.goals.tasksHeading }}
          </h2>

          <div class="flex flex-col gap-2.5">
            <GoalTaskRow
              v-for="task in detail.tasks"
              :key="task.id"
              :task="task"
              detailed
              @toggle="toggleTask"
            />
          </div>
        </section>

        <section
          v-if="detail.team.length > 0"
          class="mt-6"
          aria-labelledby="goal-team-heading"
        >
          <h2
            id="goal-team-heading"
            class="mb-3 px-0.5 text-base font-extrabold"
          >
            {{ t.goals.sharedWith }}
          </h2>

          <ul class="flex list-none flex-col gap-2.5 p-0">
            <li
              v-for="member in detail.team"
              :key="member.person.id"
              class="q2-card flex items-center gap-3 px-3 py-3"
              data-testid="goal-team-member"
            >
              <AppAvatar
                :initials="member.person.initials"
                :color="member.person.avatarColor"
                :size="40"
                :online="member.person.isOnline"
              />

              <div class="min-w-0 flex-1">
                <p class="truncate text-sm font-bold">
                  {{ member.person.displayName }}
                </p>
                <p class="text-[11px] font-semibold text-(--ui-text-muted)">
                  {{ member.streak > 0 ? t.friends.streak(member.streak) : t.goals.thisWeek }}
                </p>
              </div>
            </li>
          </ul>
        </section>
      </article>
    </div>
  </div>
</template>
