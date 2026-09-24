<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'
import type { CreateGoalRequest, Friend, GoalScheduleRequest } from '~/api/types'
import { goalIcons } from '~/api/types'
import type { GoalTemplate } from '~/utils/goalTemplates'

/**
 * The sheet that slides up from the bottom to create a goal.
 *
 * It owns its own draft and nothing else: submitting emits the request and the
 * page decides what to do with it. Server-side field errors are passed back in
 * via `error`, so the messages the user reads are the ones the API produced —
 * no second copy of the rules living in the browser.
 *
 * **Four short steps rather than one long form.** What, how often, who checks
 * it, and a summary. Every one fits on a phone without scrolling past three
 * other questions, the friend list gets a search of its own, and the last step
 * says what is about to happen — "Dein erstes Fenster endet heute um
 * Mitternacht" — before anything is committed to.
 *
 * **Somebody has to check it.** A goal is made in front of friends, and they
 * are chosen here: the goal's conversation is opened with exactly them, and
 * they are who votes on its photographs. Without a friend there is nothing to
 * choose, so the sheet offers the invite link instead of a form that could
 * only be refused (docs/adr/0027-goal-conversations.md).
 */
const props = withDefaults(defineProps<{
  friends: Friend[]
  friendsLoading?: boolean
  submitting?: boolean
  error?: ApiFailure | null
}>(), {
  friendsLoading: false,
  submitting: false,
  error: null,
})

const emit = defineEmits<{ submit: [request: CreateGoalRequest] }>()

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()
const now = useNow()
const zone = useTimeZoneOffset()

const maxTitleLength = 120
const stepCount = 4

const step = ref(0)
const title = ref('')
const icon = ref<string>(goalIcons[0]!)
const withReminder = ref(true)
const reminderTime = ref('09:00')
const participantIds = ref<string[]>([])
const friendQuery = ref('')

/*
 * Every day, which is what most people mean by a new habit and the one the
 * server assumes when a request says nothing about it.
 */
const schedule = ref<GoalScheduleRequest>({ kind: 'Interval', everyDays: 1 })

function useTemplate(template: GoalTemplate) {
  title.value = t.value.create.templates[template.key]
  icon.value = template.icon
  schedule.value = { ...template.schedule }
}

function toggleFriend(personId: string) {
  participantIds.value = participantIds.value.includes(personId)
    ? participantIds.value.filter(id => id !== personId)
    : [...participantIds.value, personId]
}

/** Only worth a search field once the list is longer than a screen. */
const showFriendSearch = computed(() => props.friends.length > 6)

const shownFriends = computed(() => {
  const query = friendQuery.value.trim().toLowerCase()
  if (!query) return props.friends

  return props.friends.filter(friend =>
    friend.person.displayName.toLowerCase().includes(query)
    || friend.person.handle.toLowerCase().includes(query))
})

const chosenNames = computed(() => props.friends
  .filter(friend => participantIds.value.includes(friend.person.id))
  .map(friend => friend.person.displayName)
  .join(', '))

const scheduleText = computed(() => scheduleLabel({
  kind: schedule.value.kind ?? 'Interval',
  everyDays: schedule.value.everyDays ?? null,
  weekdays: schedule.value.weekdays ?? [],
  times: schedule.value.times ?? null,
  period: schedule.value.period ?? null,
}, t.value))

/** The sentence that says what happens the moment the goal exists. */
const firstWindow = computed(() => {
  const today = localDay(now.value, zone.value)
  const end = firstWindowEnd(schedule.value, today)

  if (end === today) return t.value.create.firstWindowToday
  if (end === addDays(today, 1)) return t.value.create.firstWindowTomorrow
  return t.value.create.firstWindowOn(formatWeekdayDay(end, t.value))
})

/** Server-reported messages for one field, matched case-insensitively. */
function fieldError(field: string): string | undefined {
  const errors = props.error?.fieldErrors ?? {}
  const match = Object.keys(errors).find(key => key.toLowerCase() === field.toLowerCase())
  return match ? errors[match]?.[0] : undefined
}

/*
 * A refusal from the server goes back to the step that holds the field, so the
 * message is read next to what it is about rather than on a summary.
 */
watch(() => props.error, (failure) => {
  if (!failure) return
  if (fieldError('title') || fieldError('icon')) step.value = 0
  else if (fieldError('schedule') || fieldError('reminderAt')) step.value = 1
  else if (fieldError('participantIds')) step.value = 2
})

/** Only blocks the obviously empty cases; the API remains the authority. */
const canContinue = computed(() => {
  if (step.value === 0) return title.value.trim().length > 0
  if (step.value === 1) return !withReminder.value || /^\d{2}:\d{2}$/.test(reminderTime.value)
  if (step.value === 2) return participantIds.value.length > 0
  return !props.submitting
})

function next() {
  if (!canContinue.value) return
  if (step.value < stepCount - 1) step.value++
  else onSubmit()
}

function back() {
  if (step.value > 0) step.value--
}

function onSubmit() {
  if (title.value.trim().length === 0 || participantIds.value.length === 0 || props.submitting) return

  emit('submit', {
    title: title.value.trim(),
    schedule: schedule.value,
    icon: icon.value,
    reminderAt: withReminder.value ? `${reminderTime.value}:00` : null,
    participantIds: [...participantIds.value],
  })
}

/** Called by the page after a successful create. */
function reset() {
  step.value = 0
  title.value = ''
  icon.value = goalIcons[0]!
  schedule.value = { kind: 'Interval', everyDays: 1 }
  withReminder.value = true
  reminderTime.value = '09:00'
  participantIds.value = []
  friendQuery.value = ''
}

defineExpose({ reset })
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="t.create.heading"
    :description="t.create.subtitle"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <div
        v-if="friendsLoading && friends.length === 0"
        class="flex flex-col gap-3 pb-2"
        aria-busy="true"
        aria-live="polite"
      >
        <span class="sr-only">{{ t.common.loading }}</span>
        <USkeleton class="h-12 w-full rounded-(--q2-radius-md)" />
        <USkeleton class="h-32 w-full rounded-(--q2-radius-md)" />
      </div>

      <div
        v-else-if="friends.length === 0"
        class="flex flex-col gap-3 pb-2"
        data-testid="goal-create-no-friends"
      >
        <p class="text-[13px] leading-relaxed font-semibold text-(--ui-text-muted)">
          {{ t.create.noFriendsHint }}
        </p>

        <InviteCard />
      </div>

      <form
        v-else
        class="flex flex-col gap-4 pb-2"
        novalidate
        data-testid="goal-create-form"
        :data-step="step"
        @submit.prevent="next"
      >
        <!-- Where you are: four short bars and the step's name. -->
        <div>
          <div
            class="flex gap-1.5"
            aria-hidden="true"
          >
            <span
              v-for="index in stepCount"
              :key="index"
              class="h-1 flex-1 rounded-full"
              :class="index - 1 <= step ? 'bg-(--ui-text)' : 'bg-(--ui-bg-accented)'"
            />
          </div>
          <p
            class="mt-2 text-[11px] font-bold tracking-wide text-(--ui-text-muted) uppercase"
            aria-live="polite"
            data-testid="goal-create-step"
          >
            {{ t.create.stepOf(step + 1, stepCount) }} · {{ t.create.steps[step] }}
          </p>
        </div>

        <!-- 1 — what -->
        <template v-if="step === 0">
          <UFormField
            :label="t.create.titleLabel"
            name="title"
            required
            :error="fieldError('title')"
            :hint="`${title.length}/${maxTitleLength}`"
          >
            <UInput
              v-model="title"
              :placeholder="t.create.titlePlaceholder"
              :maxlength="maxTitleLength"
              autocomplete="off"
              size="xl"
              class="w-full"
              data-testid="goal-title-input"
            />
          </UFormField>

          <div v-if="title.trim().length === 0">
            <p class="mb-2 text-sm font-semibold text-(--ui-text-muted)">
              {{ t.create.templatesLabel }}
            </p>
            <ul class="flex list-none flex-wrap gap-2 p-0">
              <li
                v-for="template in goalTemplates"
                :key="template.key"
              >
                <button
                  type="button"
                  class="q2-press inline-flex min-h-10 items-center gap-1.5 rounded-full border border-(--ui-border) bg-(--q2-surface) px-3 text-[13px] font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
                  :data-testid="`goal-template-${template.key}`"
                  @click="useTemplate(template)"
                >
                  <UIcon
                    :name="goalIconName(template.icon)"
                    class="size-4 text-(--ui-text-muted)"
                    aria-hidden="true"
                  />
                  {{ t.create.templates[template.key] }}
                </button>
              </li>
            </ul>
          </div>

          <UFormField
            :label="t.create.iconLabel"
            name="icon"
            :error="fieldError('icon')"
          >
            <div
              class="grid grid-cols-6 gap-2"
              role="radiogroup"
              :aria-label="t.create.iconLabel"
            >
              <button
                v-for="option in goalIcons"
                :key="option"
                type="button"
                role="radio"
                :aria-checked="icon === option"
                :aria-label="option"
                class="flex aspect-square items-center justify-center rounded-(--q2-radius-md) border focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
                :class="icon === option
                  ? 'border-transparent bg-(--q2-accent-solid) text-(--q2-accent-contrast)'
                  : 'border-(--ui-border) bg-(--q2-surface) text-(--ui-text-muted)'"
                :data-testid="`icon-${option}`"
                @click="icon = option"
              >
                <UIcon
                  :name="goalIconName(option)"
                  class="size-5"
                />
              </button>
            </div>
          </UFormField>
        </template>

        <!-- 2 — how often, and when to be nudged -->
        <template v-else-if="step === 1">
          <UFormField
            :label="t.create.scheduleLabel"
            name="schedule"
            :error="fieldError('schedule')"
          >
            <SchedulePicker v-model="schedule" />
          </UFormField>

          <div class="flex flex-col gap-3 rounded-(--q2-radius-lg) border border-(--ui-border) bg-(--q2-surface) px-4 py-3">
            <div class="flex items-center justify-between">
              <span class="flex items-center gap-2.5">
                <UIcon
                  name="i-lucide-bell"
                  class="size-5 text-(--ui-text-muted)"
                  aria-hidden="true"
                />
                <span class="text-sm font-semibold">{{ t.create.reminderLabel }}</span>
              </span>

              <AppToggle
                v-model="withReminder"
                :label="t.create.reminderLabel"
              />
            </div>

            <UFormField
              v-if="withReminder"
              :label="t.create.reminderTimeLabel"
              name="reminderAt"
              :error="fieldError('reminderAt')"
            >
              <UInput
                v-model="reminderTime"
                type="time"
                size="lg"
                class="w-full"
                data-testid="goal-reminder-input"
              />
            </UFormField>
          </div>
        </template>

        <!-- 3 — who checks it -->
        <UFormField
          v-else-if="step === 2"
          :label="t.create.friendsLabel"
          name="participantIds"
          required
          :help="participantIds.length > 0 ? t.create.friendsChosen(participantIds.length) : t.create.friendsHint"
          :error="fieldError('participantIds')"
        >
          <UInput
            v-if="showFriendSearch"
            v-model="friendQuery"
            icon="i-lucide-search"
            :placeholder="t.create.friendsSearch"
            :aria-label="t.create.friendsSearch"
            autocomplete="off"
            size="lg"
            class="mb-2 w-full"
            data-testid="goal-friend-search"
          />

          <ul
            class="flex max-h-[40vh] list-none flex-col gap-1.5 overflow-y-auto p-0"
            data-testid="goal-friend-picker"
          >
            <li
              v-for="friend in shownFriends"
              :key="friend.person.id"
            >
              <label
                class="flex min-h-11 w-full cursor-pointer items-center gap-3 rounded-(--q2-radius-md) px-2 py-2"
                :data-testid="`goal-friend-${friend.person.id}`"
              >
                <UCheckbox
                  :model-value="participantIds.includes(friend.person.id)"
                  @update:model-value="toggleFriend(friend.person.id)"
                />

                <AppAvatar
                  :initials="friend.person.initials"
                  :color="friend.person.avatarColor"
                  :image-id="friend.person.avatarImageId"
                  :size="32"
                />

                <span
                  class="min-w-0 flex-1 truncate text-sm font-bold"
                  data-q2-private
                >
                  {{ friend.person.displayName }}
                </span>
              </label>
            </li>

            <li
              v-if="shownFriends.length === 0"
              class="px-2 py-3 text-sm font-semibold text-(--ui-text-muted)"
              role="status"
            >
              {{ t.create.friendsNoMatch }}
            </li>
          </ul>
        </UFormField>

        <!-- 4 — what is about to happen -->
        <section
          v-else
          class="flex flex-col gap-3"
          data-testid="goal-create-review"
        >
          <dl
            class="flex flex-col divide-y divide-(--ui-border-muted) rounded-(--q2-radius-lg) border border-(--ui-border) bg-(--q2-surface) px-4"
            data-q2-private
          >
            <div class="flex items-center gap-3 py-3">
              <UIcon
                :name="goalIconName(icon)"
                class="size-5 shrink-0 text-(--ui-text-muted)"
                aria-hidden="true"
              />
              <dt class="sr-only">
                {{ t.create.reviewTitle }}
              </dt>
              <dd class="min-w-0 flex-1 text-[15px] font-extrabold">
                {{ title.trim() }}
              </dd>
            </div>
            <div class="flex justify-between gap-3 py-3 text-sm">
              <dt class="font-semibold text-(--ui-text-muted)">
                {{ t.create.reviewSchedule }}
              </dt>
              <dd class="text-end font-bold">
                {{ scheduleText }}
              </dd>
            </div>
            <div class="flex justify-between gap-3 py-3 text-sm">
              <dt class="font-semibold text-(--ui-text-muted)">
                {{ t.create.reviewFriends }}
              </dt>
              <dd class="min-w-0 text-end font-bold">
                {{ chosenNames }}
              </dd>
            </div>
            <div class="flex justify-between gap-3 py-3 text-sm">
              <dt class="font-semibold text-(--ui-text-muted)">
                {{ t.create.reviewReminder }}
              </dt>
              <dd class="text-end font-bold">
                {{ withReminder ? reminderTime : t.create.reviewNoReminder }}
              </dd>
            </div>
          </dl>

          <p
            class="flex items-start gap-2 text-[13px] leading-relaxed font-semibold text-(--ui-text-muted)"
            data-testid="goal-first-window"
          >
            <UIcon
              name="i-lucide-hourglass"
              class="mt-0.5 size-4 shrink-0"
              aria-hidden="true"
            />
            {{ firstWindow }}
          </p>
        </section>

        <!--
          A non-field error (offline, 5xx) belongs above the button where the
          user is looking after pressing it.
        -->
        <p
          v-if="error && Object.keys(error.fieldErrors).length === 0"
          class="text-sm text-(--ui-error)"
          role="alert"
          data-testid="goal-create-error"
        >
          {{ t.errors[error.kind] }}
        </p>

        <div class="flex gap-2.5">
          <UButton
            v-if="step > 0"
            type="button"
            size="xl"
            color="neutral"
            variant="outline"
            icon="i-lucide-chevron-left"
            class="justify-center"
            :disabled="submitting"
            data-testid="goal-back"
            @click="back"
          >
            {{ t.common.back }}
          </UButton>

          <UButton
            type="submit"
            :icon="step === stepCount - 1 ? 'i-lucide-sparkles' : undefined"
            :trailing-icon="step === stepCount - 1 ? undefined : 'i-lucide-chevron-right'"
            size="xl"
            class="flex-1 justify-center"
            :loading="step === stepCount - 1 && submitting"
            :disabled="!canContinue"
            :data-testid="step === stepCount - 1 ? 'goal-submit' : 'goal-next'"
          >
            {{ step === stepCount - 1 ? t.create.submit : t.create.next }}
          </UButton>
        </div>
      </form>
    </template>
  </UDrawer>
</template>
