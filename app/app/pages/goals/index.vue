<script setup lang="ts">
import type { CreateGoalRequest, Image, SaveGoalDraftRequest } from '~/api/types'
import type { PhotoOrigin } from '~/utils/proofSource'

/**
 * The Dos tab — "Dos & Don'ts" on the screen: the Dos under one segment, the
 * Don'ts under the other (#77).
 *
 * The plus in the header is the one way a goal is made. It lives here rather
 * than in the tab bar, because a plus in the tab bar was the only visible way
 * to reach this screen at all, and people read it as "this is where my goals
 * are" rather than "make a new one".
 *
 * The segment lives in the URL rather than in component state, so a view can
 * be shared and survives a reload. `replace`, not `push`: switching is looking
 * at the same screen from another angle, and it should not take four presses
 * of the back button to leave the page. Dos are the default and write nothing;
 * the addresses of the tabs this screen had before — `?tab=all`, and
 * `?tab=goals` from when it said "Ziele" (#60) — therefore still land.
 *
 * Each segment is split the way the chat list is (docs/adr/0027): yours, where
 * you deliver, and your friends', which you only check. Yours start with what
 * is due today, carrying its camera or log button, because there is no "Heute"
 * tab any more to put them under (`goalListSections`).
 */
type Tab = 'dos' | 'donts'

const route = useRoute()
const router = useRouter()
const t = useMessages()
const now = useNow()
const zone = useTimeZoneOffset()

/** The reader's day, for a Don't's "seit X Tagen". */
const today = computed(() => localDay(now.value, zone.value))

const tab = computed<Tab>({
  get: () => (route.query.tab === 'donts' ? 'donts' : 'dos'),
  set: value => router.replace({ query: value === 'donts' ? { tab: 'donts' } : {} }),
})

/**
 * The create sheet is a query parameter too, so the open sheet survives a
 * reload and closes with the back gesture, which is what a person expects of
 * something that covers the screen.
 */
const isSheetOpen = computed({
  get: () => route.query.create === '1',
  set: (value) => {
    const query = { ...route.query }
    if (value) query.create = '1'
    else delete query.create
    router.replace({ query })
  },
})

const { goals, due, error, isLoading, refresh, create, isCreating, createError } = useGoals()

/*
 * The Do somebody without friends keeps waiting (docs/adr/0039). Shown above
 * the list until the first friend arrives and the server turns it into the
 * goal that then appears in it.
 */
const drafts = useGoalDraft()
const { share: shareInvite } = useInvite()
const toast = useToastMessage()

/** The segment's goals, yours and your friends' apart. An empty half is not shown. */
const sections = computed(() => {
  const { mine, friends } = goalListSections(goals.value, due.value, tab.value === 'donts' ? 'Dont' : 'Do')

  return [
    { key: 'mine', title: t.value.goals.sectionMine, hint: t.value.goals.sectionMineHint, rows: mine },
    { key: 'friends', title: t.value.goals.sectionFriends, hint: t.value.goals.sectionFriendsHint, rows: friends },
  ].filter(section => section.rows.length > 0)
})

// Who can be asked to check a new goal. The sheet only offers friends, which
// is the server's rule too.
const { friends, isLoading: friendsLoading } = useFriends()
const { isDelivering, deliver, maxEdge } = useProofDelivery()
const { isLogging, logUse } = useDontUse(() => refresh())

/** One tap from the row; the row has changed either way, so read it again. */
async function onLog(goalId: string) {
  if (await logUse(goalId)) await refresh()
}

/** Which goal the camera is open for — one camera per screen, never per row. */
const deliveringFor = ref<string | null>(null)

/** The same hand-in as the start screen's — see pages/index.vue. */
async function handIn(image: Image, origin: PhotoOrigin) {
  const goalId = deliveringFor.value
  if (!goalId) return null

  const result = await deliver(goalId, image, origin)
  if (result.status !== 'moved') await refresh()

  return result.status === 'refused' ? result.message : null
}

const sheet = useTemplateRef('sheet')

const tabs = computed(() => [
  { value: 'dos' as const, label: t.value.goals.tabDos },
  { value: 'donts' as const, label: t.value.goals.tabDonts },
])

/** Keeps a Do waiting, and shows it where the list will have it once it starts. */
async function onDraft(request: SaveGoalDraftRequest) {
  if (!(await drafts.save(request))) return

  toast.show(t.value.toast.draftSaved)

  // Closes the sheet and lands on Dos, where the waiting card is.
  await router.replace({ query: {} })
}

async function onDiscardDraft() {
  if (await drafts.discard()) toast.show(t.value.toast.draftDiscarded)
}

/** Lands on the segment the new one is in, which is where it is looked for next. */
async function onCreate(request: CreateGoalRequest) {
  const created = await create(request)
  if (!created) return

  sheet.value?.reset()
  await router.replace({ query: created.kind === 'Dont' ? { tab: 'donts' } : {} })
}

useHead({ title: () => t.value.goals.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <!-- The one way a goal is made. Not repeated in the empty state below:
         two of them would put the screen's loudest control in two places. -->
    <AppScreenHeader :title="t.goals.heading">
      <template #actions>
        <UButton
          icon="i-lucide-plus"
          size="lg"
          :ui="{ base: 'size-11 justify-center rounded-full' }"
          :aria-label="t.create.open"
          data-testid="open-create-goal"
          @click="isSheetOpen = true"
        />
      </template>
    </AppScreenHeader>

    <AppContentPanel :refresh="refresh">
      <template #toolbar>
        <AppSegmented
          v-model="tab"
          :options="tabs"
          :label="t.goals.heading"
        />
      </template>
      <div
        v-if="isLoading"
        class="flex flex-col gap-3"
        aria-busy="true"
        aria-live="polite"
      >
        <span class="sr-only">{{ t.common.loading }}</span>
        <USkeleton
          v-for="index in 3"
          :key="index"
          class="h-20 w-full rounded-(--q2-radius-lg)"
        />
      </div>

      <AppErrorState
        v-else-if="error"
        :error="error"
        retryable
        @retry="refresh()"
      />

      <template v-else>
        <GoalDraftCard
          v-if="drafts.draft.value && tab === 'dos'"
          class="mb-5"
          :draft="drafts.draft.value"
          :busy="drafts.isSaving.value"
          @share="shareInvite()"
          @discard="onDiscardDraft"
        />

        <div
          v-if="sections.length > 0"
          class="flex flex-col gap-5"
          data-testid="goal-list"
        >
          <section
            v-for="section in sections"
            :key="section.key"
            :aria-labelledby="`goal-section-${section.key}`"
            :data-testid="`goal-section-${section.key}`"
          >
            <h2
              :id="`goal-section-${section.key}`"
              class="flex items-baseline justify-between gap-2 px-0.5 pb-1.5"
            >
              <span class="q2-eyebrow">{{ section.title }}</span>
              <span class="text-[11px] font-semibold text-(--ui-text-dimmed)">{{ section.hint }}</span>
            </h2>

            <ul class="flex list-none flex-col gap-2.5 p-0">
              <li
                v-for="{ goal, isDue } in section.rows"
                :key="goal.id"
              >
                <!-- Your own window that covers today carries the one thing to
                     do now; everything else is a row you open. -->
                <DontWindowRow
                  v-if="isDue && goal.isMine && goal.kind === 'Dont'"
                  :goal="goal"
                  :busy="isDelivering || isLogging"
                  @deliver="deliveringFor = $event"
                  @log="onLog"
                />
                <GoalWindowRow
                  v-else-if="isDue && goal.isMine"
                  :goal="goal"
                  :now="now"
                  :busy="isDelivering"
                  @deliver="deliveringFor = $event"
                />
                <GoalCard
                  v-else
                  :goal="goal"
                  :today="today"
                />
              </li>
            </ul>
          </section>
        </div>

        <AppStateMessage
          v-else-if="!(drafts.draft.value && tab === 'dos')"
          :icon="tab === 'donts' ? 'i-lucide-ban' : 'i-lucide-target'"
          :title="tab === 'donts' ? t.goals.noDonts : t.goals.noDos"
          :description="tab === 'donts' ? t.goals.noDontsHint : t.goals.noDosHint"
          data-testid="goals-empty"
        />

        <!-- The way to what has stopped. At the foot of the screen rather
             than in the header: the archive is somewhere you go looking, not
             somewhere you are sent. It stays here with no goals left, because
             that is exactly when everything is in it. -->
        <UButton
          to="/goals/archive"
          class="mt-4 min-h-11 w-full justify-center"
          size="lg"
          color="neutral"
          variant="ghost"
          icon="i-lucide-archive"
          :label="t.goals.archive"
          data-testid="goals-archive-link"
        />
      </template>
    </AppContentPanel>

    <GoalCreateSheet
      ref="sheet"
      v-model:open="isSheetOpen"
      :friends="friends"
      :friends-loading="friendsLoading"
      :submitting="isCreating || drafts.isSaving.value"
      :error="createError"
      :draft="drafts.draft.value"
      :draft-error="drafts.saveError.value"
      @submit="onCreate"
      @draft="onDraft"
    />

    <PhotoCapture
      :open="deliveringFor !== null"
      purpose="Proof"
      labels-source
      :max-edge="maxEdge"
      :hand-in="handIn"
      @update:open="value => { if (!value) deliveringFor = null }"
    />
  </div>
</template>
