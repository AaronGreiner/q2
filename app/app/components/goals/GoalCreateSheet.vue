<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'
import type { CreateGoalRequest, Friend, GoalScheduleRequest } from '~/api/types'
import { goalIcons } from '~/api/types'

/**
 * The sheet that slides up from the bottom to create a goal.
 *
 * It owns its own draft and nothing else: submitting emits the request and the
 * page decides what to do with it. Server-side field errors are passed back in
 * via `error`, so the messages the user reads are the ones the API produced —
 * no second copy of the rules living in the browser.
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

const maxTitleLength = 120

const title = ref('')
const icon = ref<string>(goalIcons[0]!)
const withReminder = ref(true)
const participantIds = ref<string[]>([])

function toggleFriend(personId: string) {
  participantIds.value = participantIds.value.includes(personId)
    ? participantIds.value.filter(id => id !== personId)
    : [...participantIds.value, personId]
}

/*
 * Every day, which is what most people mean by a new habit and the one the
 * server assumes when a request says nothing about it.
 */
const schedule = ref<GoalScheduleRequest>({ kind: 'Interval', everyDays: 1 })

/** Server-reported messages for one field, matched case-insensitively. */
function fieldError(field: string): string | undefined {
  const errors = props.error?.fieldErrors ?? {}
  const match = Object.keys(errors).find(key => key.toLowerCase() === field.toLowerCase())
  return match ? errors[match]?.[0] : undefined
}

/** Only blocks the obviously empty cases; the API remains the authority. */
const canSubmit = computed(() =>
  title.value.trim().length > 0 && participantIds.value.length > 0 && !props.submitting)

function onSubmit() {
  if (!canSubmit.value) return

  emit('submit', {
    title: title.value.trim(),
    schedule: schedule.value,
    icon: icon.value,

    // A daily nudge at nine, or none at all. Choosing the hour is a setting
    // this version does not have, and inventing a time picker for it would be
    // building ahead of the requirement.
    reminderAt: withReminder.value ? '09:00:00' : null,
    participantIds: [...participantIds.value],
  })
}

/** Called by the page after a successful create. */
function reset() {
  title.value = ''
  icon.value = goalIcons[0]!
  schedule.value = { kind: 'Interval', everyDays: 1 }
  withReminder.value = true
  participantIds.value = []
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
        @submit.prevent="onSubmit"
      >
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

        <UFormField
          :label="t.create.friendsLabel"
          name="participantIds"
          required
          :help="participantIds.length > 0 ? t.create.friendsChosen(participantIds.length) : t.create.friendsHint"
          :error="fieldError('participantIds')"
        >
          <ul
            class="flex max-h-[32vh] list-none flex-col gap-1.5 overflow-y-auto p-0"
            data-testid="goal-friend-picker"
          >
            <li
              v-for="friend in friends"
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
          </ul>
        </UFormField>

        <UFormField
          :label="t.create.scheduleLabel"
          name="schedule"
          :error="fieldError('schedule')"
        >
          <SchedulePicker v-model="schedule" />
        </UFormField>

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

        <div class="flex items-center justify-between rounded-(--q2-radius-lg) border border-(--ui-border) bg-(--q2-surface) px-4 py-3">
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

        <UButton
          type="submit"
          icon="i-lucide-sparkles"
          size="xl"
          class="justify-center"
          :loading="submitting"
          :disabled="!canSubmit"
          data-testid="goal-submit"
        >
          {{ t.create.submit }}
        </UButton>
      </form>
    </template>
  </UDrawer>
</template>
