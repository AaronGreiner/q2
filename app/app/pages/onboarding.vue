<script setup lang="ts">
import type { HandleCheck, Image, LanguagePreference, ThemePreference } from '~/api/types'
import type { AccentName } from '~/composables/useAccent'
import type { DraftFormValue } from '~/components/goals/GoalDraftForm.vue'
import type { OnboardingSummary } from '~/components/onboarding/OnboardingDone.vue'
import type { ProfileChoice } from '~/components/onboarding/OnboardingProfile.vue'

/**
 * The first-run introduction (docs/adr/0039-first-run-onboarding.md).
 *
 * Every account goes through it once, on whichever device it signs in first,
 * and never again: the route guard keeps somebody here until the server has
 * been told it is over (`middleware/auth.global.ts`). Mandatory, but not a
 * toll booth — every step that asks for something optional can be put off.
 *
 * The page owns everything that reads or writes; the steps are components that
 * show and ask. Nothing the introduction changes is introduction-specific
 * except the handle and its end: the photograph, the name, the theme, the
 * waiting Do, the invite link and the push permission all go through the same
 * composables as the rest of the app, so a choice made here is the same choice
 * the settings screen would have made.
 *
 * The step is in the address, so a reload stays where it was and the back
 * gesture goes one step back rather than out of the app.
 */
definePageMeta({ layout: 'plain' })

type StepName = 'welcome' | 'profile' | 'appearance' | 'firstDo' | 'invite' | 'notifications' | 'done'

const route = useRoute()
const router = useRouter()
const t = useMessages()
const api = useQ2Api()
const { report } = useErrorReporter()

const session = useSession()
const { profile, rename, chooseAvatar, refresh: refreshProfile } = useProfile()
const { friends } = useFriends()
const { goals, create, isCreating, createError } = useGoals()
const drafts = useGoalDraft()
const invite = useInvite()
const push = usePushNotifications()
const { update: updateSettings } = useAppSettings()
const theme = useTheme()
const language = useLanguage()
const accent = useAccent()

/*
 * Notifications are offered only where asking can work and has not been
 * answered: the iOS app has no Web Push yet, a deployment without keys sends
 * none, and a browser that already said yes or no has nothing to be asked.
 * Worked out after hydration — the server cannot know what this browser
 * allows — and kept once offered, so saying yes does not make the step vanish.
 */
const pushOffered = ref(false)

onMounted(async () => {
  await push.resolve()
  pushOffered.value = push.state.value === 'off'
})

const steps = computed<StepName[]>(() => [
  'welcome',
  'profile',
  'appearance',
  'firstDo',
  'invite',
  ...(pushOffered.value ? ['notifications' as const] : []),
  'done',
])

const current = computed<StepName>(() => {
  const asked = route.query.step

  if (typeof asked === 'string' && steps.value.includes(asked as StepName)) return asked as StepName

  // An address left on the notifications step, on a device that cannot ask.
  return asked === 'notifications' ? 'done' : 'welcome'
})

const position = computed(() => steps.value.indexOf(current.value) + 1)
const total = computed(() => steps.value.length)

function go(step: StepName) {
  return router.push({ query: step === 'welcome' ? {} : { step } })
}

function next() {
  const following = steps.value[position.value]
  if (following) return go(following)
}

function back() {
  const previous = steps.value[position.value - 2]
  if (previous) return go(previous)
}

/* --- who: the person, as the session and the profile know them --- */

const person = computed(() => {
  const signedIn = session.person.value
  if (!signedIn) return null

  // The handle comes from the session, which choosing one updates; the
  // picture from the profile, which choosing one updates.
  return { ...signedIn, avatarImageId: profile.value?.person.avatarImageId ?? signedIn.avatarImageId }
})

/* --- welcome and appearance: applied at once, remembered on the server --- */

function setLanguage(value: LanguagePreference) {
  updateSettings({ language: value })
}

function setTheme(value: ThemePreference) {
  updateSettings({ theme: value })
}

function setAccent(value: AccentName) {
  accent.value = value
}

async function signOut() {
  await session.logout()
  await navigateTo('/login', { replace: true })
}

/* --- profile: the photograph, the name, and the handle while it may change --- */

const capturing = ref(false)

async function onPhoto(image: Image) {
  capturing.value = false
  await chooseAvatar(image.id)
}

const handleCheck = ref<HandleCheck | null>(null)
const isChecking = ref(false)
let checkTimer: ReturnType<typeof setTimeout> | undefined
let checkRound = 0

/**
 * Asks whether a handle is free, once typing pauses. Only the newest question's
 * answer counts: a slow reply about "nor" must not land after the one about
 * "nora".
 */
function onCheck(handle: string) {
  isChecking.value = true
  clearTimeout(checkTimer)

  const round = ++checkRound

  checkTimer = setTimeout(async () => {
    try {
      const answer = await api.onboarding.checkHandle(handle)
      if (round === checkRound) handleCheck.value = answer
    }
    catch (caught) {
      report(caught, { feature: 'onboarding', action: 'checkHandle' })
    }
    finally {
      if (round === checkRound) isChecking.value = false
    }
  }, 300)
}

onBeforeUnmount(() => clearTimeout(checkTimer))

const isSavingProfile = ref(false)
const profileFailed = ref(false)

async function onSaveProfile(choice: ProfileChoice) {
  const signedIn = session.person.value
  if (!signedIn || isSavingProfile.value) return

  isSavingProfile.value = true
  profileFailed.value = false

  try {
    if (choice.displayName !== signedIn.displayName && !(await rename(choice.displayName))) {
      profileFailed.value = true
      return
    }

    if (choice.handle !== signedIn.handle) {
      await session.chooseHandle(choice.handle)
    }

    // The session carries the name too, and the summary at the end reads it.
    await session.adopt(await api.accounts.session())
    await refreshProfile()
    await next()
  }
  catch (caught) {
    report(caught, { feature: 'onboarding', action: 'saveProfile' })
    profileFailed.value = true

    // Taken in the meantime, most likely: ask again so the field says so.
    onCheck(choice.handle)
  }
  finally {
    isSavingProfile.value = false
  }
}

/* --- the first Do: a goal with a friend, a waiting Do without --- */

const ownGoalCount = computed(() => goals.value.filter(goal => goal.isMine).length)

const firstDoError = computed(() => (friends.value.length > 0 ? createError.value : drafts.saveError.value))

async function onFirstDo(value: DraftFormValue) {
  const saved = friends.value.length > 0
    ? await create(value)
    : await drafts.save({ title: value.title, icon: value.icon, schedule: value.schedule, reminderAt: value.reminderAt })

  if (saved) await next()
}

/* --- the invite link --- */

const hasShared = ref(false)

async function onShare() {
  await invite.share()
  hasShared.value = true
}

async function onCopy() {
  await invite.copy()
  hasShared.value = true
}

/* --- the end --- */

const summary = computed<OnboardingSummary>(() => ({
  hasPhoto: Boolean(person.value?.avatarImageId),
  handle: person.value?.handle ?? '',
  firstDo: ownGoalCount.value > 0 ? 'running' : drafts.draft.value ? 'waiting' : 'none',
  friendCount: friends.value.length,
  notifications: pushOffered.value ? push.state.value === 'on' : null,
}))

const isFinishing = ref(false)
const finishFailed = ref(false)

/**
 * Tells the server, then opens the app with the tour over it. The tour is a
 * query parameter rather than a stored flag: it is shown once, right now, and
 * a reload in the middle of it showing it again is no harm.
 *
 * Resolves to whether it went through; a failure has been reported.
 */
async function finish(): Promise<boolean> {
  try {
    await session.completeOnboarding()
    await navigateTo({ path: '/', query: { tour: '1' } }, { replace: true })
    return true
  }
  catch (caught) {
    report(caught, { feature: 'onboarding', action: 'complete' })
    return false
  }
}

async function onFinish() {
  if (isFinishing.value) return

  isFinishing.value = true

  // Cleared first, so a retry does not show the last failure while it runs.
  finishFailed.value = false
  finishFailed.value = !(await finish())
  isFinishing.value = false
}

/*
 * Out of the introduction from the invite step, for somebody who does not
 * want to invite anybody yet: the waiting Do lapses — it was waiting for a
 * friend who is not being asked — and the rest of the steps are skipped. The
 * tour still comes; it is about the app, not about friends.
 *
 * The Do goes first. Were the introduction ended first and the discard then
 * failed, a Do the person had just let go of would start by itself the day a
 * friend turned up.
 */
const isLeaving = ref(false)
const leaveFailed = ref(false)

async function onLeave() {
  if (isLeaving.value) return

  isLeaving.value = true
  leaveFailed.value = false

  const lapsed = !drafts.draft.value || await drafts.discard()
  leaveFailed.value = !lapsed || !(await finish())
  isLeaving.value = false
}

useHead({ title: () => t.value.onboarding.title })
</script>

<template>
  <div
    class="flex min-h-0 flex-1 flex-col"
    data-testid="onboarding"
    :data-current="current"
  >
    <OnboardingWelcome
      v-if="current === 'welcome'"
      :step="position"
      :total="total"
      :language="language"
      @update:language="setLanguage"
      @next="next"
      @skip="go('profile')"
      @sign-out="signOut"
    />

    <template v-else-if="current === 'profile'">
      <OnboardingProfile
        v-if="person"
        :step="position"
        :total="total"
        :person="person"
        :handle-check="handleCheck"
        :is-checking="isChecking"
        :is-saving="isSavingProfile"
        :failed="profileFailed"
        @back="back"
        @capture="capturing = true"
        @check="onCheck"
        @save="onSaveProfile"
      />

      <PhotoCapture
        v-model:open="capturing"
        purpose="Avatar"
        @uploaded="onPhoto"
      />
    </template>

    <OnboardingAppearance
      v-else-if="current === 'appearance'"
      :step="position"
      :total="total"
      :theme="theme.preference.value"
      :accent="accent"
      :language="language"
      @update:theme="setTheme"
      @update:accent="setAccent"
      @update:language="setLanguage"
      @back="back"
      @next="next"
    />

    <OnboardingFirstDo
      v-else-if="current === 'firstDo'"
      :step="position"
      :total="total"
      :friends="friends"
      :draft="drafts.draft.value"
      :own-goal-count="ownGoalCount"
      :is-saving="isCreating || drafts.isSaving.value"
      :error="firstDoError"
      @back="back"
      @skip="next"
      @next="next"
      @submit="onFirstDo"
    />

    <OnboardingInvite
      v-else-if="current === 'invite'"
      :step="position"
      :total="total"
      :url="invite.url.value"
      :is-loading="invite.isLoading.value"
      :friend-names="friends.map(friend => friend.person.displayName)"
      :draft="drafts.draft.value"
      :has-shared="hasShared"
      :is-leaving="isLeaving"
      :leave-failed="leaveFailed"
      @back="back"
      @next="next"
      @share="onShare"
      @copy="onCopy"
      @leave="onLeave"
    />

    <OnboardingNotifications
      v-else-if="current === 'notifications'"
      :step="position"
      :total="total"
      :state="push.state.value"
      :is-busy="push.isBusy.value"
      @back="back"
      @next="next"
      @allow="push.enable()"
    />

    <OnboardingDone
      v-else
      :step="position"
      :total="total"
      :summary="summary"
      :is-finishing="isFinishing"
      :failed="finishFailed"
      @back="back"
      @finish="onFinish"
    />
  </div>
</template>
