<script setup lang="ts">
import type { CreateGoalRequest, Goal } from '~/api/types'

/**
 * The archive: goals that have stopped.
 *
 * Deliberately its own screen rather than a filter on the goals list. What is
 * here has no future — no deadline, no camera, no warning — and mixing it into
 * the list of things somebody still owes would make the list longer without
 * making it more useful.
 *
 * It is also the only place a goal can really be deleted, and that is the
 * second half of the same decision: stopping keeps everything, deleting keeps
 * nothing. Two steps, in that order, so nobody destroys half a year of record
 * by pressing one button.
 *
 * Yours and your friends' apart, a friend's row naming whose it was, and the
 * two numbers said in words (#102). One of your own can be started again: the
 * create sheet opens filled in from it, on its summary, so it is two taps —
 * and the ended goal keeps its record here.
 */
definePageMeta({ layout: 'plain' })

const t = useMessages()

const router = useRouter()
const { goals, error, isLoading, isRemoving, isRestarting, restartError, refresh, remove, restart } = useGoalArchive()

/** Yours first, then the ones you were checking. An empty half is not shown. */
const sections = computed(() => [
  { key: 'mine', title: t.value.archive.sectionMine, rows: goals.value.filter(goal => goal.isMine) },
  { key: 'friends', title: t.value.archive.sectionFriends, rows: goals.value.filter(goal => !goal.isMine) },
].filter(section => section.rows.length > 0))

/** What a row says under its title: the record in words, and when it ended. */
function recordLabel(goal: Goal): string {
  const parts = [t.value.archive.record(goal.windowsDone, goal.windowsMissed)]
  if (goal.kind === 'Dont' && goal.windowsRelapsed > 0) parts.push(t.value.archive.relapses(goal.windowsRelapsed))
  parts.push(closedLabel(goal))
  return parts.join(' · ')
}

/* Starting again: the friends who can check it, and the sheet. */
const { friends, isLoading: friendsLoading } = useFriends()
const restarting = ref(false)
const sheet = useTemplateRef('sheet')

function startAgain(goal: Goal) {
  sheet.value?.startFrom(goal)
  restarting.value = true
}

async function onRestart(request: CreateGoalRequest) {
  const created = await restart(request)
  if (!created) return

  restarting.value = false
  await router.push(`/goals/${created.id}`)
}

/**
 * The goal the confirmation is about, and whether the question is on screen.
 *
 * Two refs rather than one derived from the other, because the dialog closes
 * itself *before* it emits: a `deleting` cleared by closing would be null by
 * the time the answer arrived, and the delete would quietly not happen.
 */
const deleting = ref<Goal | null>(null)
const isConfirming = ref(false)

function askToDelete(goal: Goal) {
  deleting.value = goal
  isConfirming.value = true
}

function closedLabel(goal: Goal): string {
  if (!goal.closedAt) return goalScheduleLabel(goal, t.value)

  const day = formatInstantDate(goal.closedAt)

  return goal.status === 'Completed' ? t.value.archive.completedOn(day) : t.value.archive.closedOn(day)
}

async function onDelete() {
  const goal = deleting.value
  deleting.value = null

  if (goal) await remove(goal.id)
}

useHead({ title: () => t.value.archive.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader
      :title="t.archive.heading"
      :eyebrow="t.archive.subtitle"
      back-to="/goals?tab=all"
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
          v-for="row in 3"
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

      <template v-else-if="goals.length > 0">
        <p class="mb-3 px-0.5 text-[12px] font-semibold text-(--ui-text-dimmed)">
          {{ t.archive.note }}
        </p>

        <section
          v-for="section in sections"
          :key="section.key"
          class="mb-5"
          :aria-labelledby="`archive-${section.key}`"
          :data-testid="`archive-section-${section.key}`"
        >
          <h2
            :id="`archive-${section.key}`"
            class="mb-2 px-0.5 text-[13px] font-extrabold text-(--ui-text-muted)"
          >
            {{ section.title }}
          </h2>

          <ul
            class="flex list-none flex-col gap-2.5 p-0"
            data-testid="archive-list"
          >
            <li
              v-for="goal in section.rows"
              :key="goal.id"
              class="q2-card flex items-center gap-3 px-3 py-3"
              data-testid="archive-row"
            >
              <!-- Yours led by its icon, a friend's by its owner — the way a
                   running goal's card is (GoalCard). -->
              <AppAvatar
                v-if="!goal.isMine"
                :initials="goal.owner.initials"
                :color="goal.owner.avatarColor"
                :image-id="goal.owner.avatarImageId"
                :size="44"
              />
              <span
                v-else
                class="flex size-11 shrink-0 items-center justify-center rounded-(--q2-radius-md) bg-(--ui-bg-accented) text-(--ui-text-muted)"
                aria-hidden="true"
              >
                <UIcon
                  :name="goalIconName(goal.icon)"
                  class="size-5"
                />
              </span>

              <!-- `-my-2 py-2` rather than a taller row: the text is two short
                   lines, and the tap target has to be 44px even when the words
                   are not (app/AGENTS.md section 8). -->
              <NuxtLink
                :to="`/goals/${goal.id}`"
                class="-my-2 min-w-0 flex-1 py-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
              >
                <!-- Two lines rather than one: with "Neu starten" and "Löschen"
                     beside it, one line kept barely three words at 390 px. -->
                <p
                  class="line-clamp-2 text-sm font-bold text-pretty"
                  data-q2-private
                >
                  {{ goal.title }}
                </p>
                <p
                  v-if="!goal.isMine"
                  class="truncate text-[12px] font-bold"
                  data-q2-private
                  data-testid="archive-owner"
                >
                  {{ t.archive.ownedBy(goal.owner.displayName) }}
                </p>
                <p
                  class="mt-0.5 text-[11px] font-semibold text-(--ui-text-muted)"
                  data-testid="archive-record"
                >
                  {{ recordLabel(goal) }}
                </p>
              </NuxtLink>

              <!-- Only the owner may start it again or delete it, and the server
                   says who that is. A goal somebody was merely invited to stays
                   in their archive. -->
              <template v-if="goal.isMine">
                <UButton
                  class="min-h-11 min-w-11 justify-center"
                  size="sm"
                  color="neutral"
                  variant="ghost"
                  icon="i-lucide-rotate-ccw"
                  :aria-label="`${t.archive.restart}: ${goal.title}`"
                  :disabled="friendsLoading || isRestarting"
                  data-testid="archive-restart"
                  @click="startAgain(goal)"
                />
                <UButton
                  class="min-h-11 min-w-11 justify-center"
                  size="sm"
                  color="error"
                  variant="ghost"
                  icon="i-lucide-trash-2"
                  :aria-label="`${t.archive.delete}: ${goal.title}`"
                  :disabled="isRemoving"
                  data-testid="archive-delete"
                  @click="askToDelete(goal)"
                />
              </template>
            </li>
          </ul>
        </section>
      </template>

      <AppStateMessage
        v-else
        icon="i-lucide-archive"
        :title="t.archive.empty"
        :description="t.archive.emptyHint"
        data-testid="archive-empty"
      />
    </AppContentPanel>

    <GoalCreateSheet
      ref="sheet"
      v-model:open="restarting"
      :friends="friends"
      :friends-loading="friendsLoading"
      :submitting="isRestarting"
      :error="restartError"
      @submit="onRestart"
    />

    <AppConfirmDialog
      v-model:open="isConfirming"
      :title="t.archive.deleteHeading"
      :description="t.archive.deleteBody"
      :confirm-label="t.archive.deleteConfirm"
      @confirm="onDelete"
    />
  </div>
</template>
