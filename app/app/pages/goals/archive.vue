<script setup lang="ts">
import type { Goal } from '~/api/types'

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
 */
definePageMeta({ layout: 'plain' })

const t = useMessages()

const { goals, error, isLoading, isRemoving, refresh, remove } = useGoalArchive()

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
  if (!goal.closedAt) return scheduleLabel(goal.schedule, t.value)

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
      back-to="/goals?tab=goals"
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

        <ul
          class="flex list-none flex-col gap-2.5 p-0"
          data-testid="archive-list"
        >
          <li
            v-for="goal in goals"
            :key="goal.id"
            class="q2-card flex items-center gap-3 px-3 py-3"
            data-testid="archive-row"
          >
            <span
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
              <p
                class="truncate text-sm font-bold"
                data-q2-private
              >
                {{ goal.title }}
              </p>
              <p class="mt-0.5 text-[11px] font-semibold text-(--ui-text-muted)">
                {{ t.goals.balanceValue(goal.windowsDone, goal.windowsMissed) }} · {{ closedLabel(goal) }}
              </p>
            </NuxtLink>

            <!-- Only the owner may delete, and the server says who that is. A
                 goal somebody was merely invited to stays in their archive. -->
            <UButton
              v-if="goal.isMine"
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
          </li>
        </ul>
      </template>

      <AppStateMessage
        v-else
        icon="i-lucide-archive"
        :title="t.archive.empty"
        :description="t.archive.emptyHint"
        data-testid="archive-empty"
      />
    </div>

    <AppConfirmDialog
      v-model:open="isConfirming"
      :title="t.archive.deleteHeading"
      :description="t.archive.deleteBody"
      :confirm-label="t.archive.deleteConfirm"
      @confirm="onDelete"
    />
  </div>
</template>
