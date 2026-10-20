<script setup lang="ts">
import type { ApiFailure } from '~/api/errors'
import type { Friend, GoalCategory, GoalDraft, GoalScheduleRequest, ScheduleKind } from '~/api/types'
import { goalTemplates, type GoalTemplate } from '~/utils/goalTemplates'

/**
 * A first Do, in one short form: what, how often, when to be reminded — and,
 * when there is somebody, who checks it.
 *
 * Used where somebody makes their first Do: the introduction's step, and the
 * create sheet of a person with no friend yet. Without friends what comes out
 * is a waiting Do (docs/adr/0039-first-run-onboarding.md); with them it is an
 * ordinary goal. The form does not decide which — it says what was chosen and
 * the screen sends it where it belongs.
 *
 * Shorter than the create sheet on purpose. No category choice, no Don't: an
 * idea fills the category, and a first commitment is easier to make than to
 * configure.
 * No one-off either — it would need a day, and a waiting Do could miss it.
 *
 * The submit button is the screen's, outside this form, and reaches it through
 * `form-id` — the introduction keeps its buttons at the bottom edge, where a
 * thumb is. Whether it may be pressed comes back through `v-model:valid`.
 */
export interface DraftFormValue {
  title: string
  category: GoalCategory
  schedule: GoalScheduleRequest
  reminderAt: string | null
  participantIds: string[]
}

const props = withDefaults(defineProps<{
  formId: string
  friends?: Friend[]
  initial?: GoalDraft | null
  error?: ApiFailure | null
}>(), {
  friends: () => [],
  initial: null,
  error: null,
})

const emit = defineEmits<{ submit: [value: DraftFormValue] }>()

const valid = defineModel<boolean>('valid', { default: false })

const t = useMessages()

const maxTitleLength = 120
const kinds: readonly ScheduleKind[] = ['Interval', 'Weekdays', 'Times']

const title = ref(props.initial?.title ?? '')
const category = ref<GoalCategory>(props.initial?.category ?? 'Other')
const schedule = ref<GoalScheduleRequest>(props.initial
  ? {
      kind: props.initial.schedule.kind,
      everyDays: props.initial.schedule.everyDays,
      weekdays: props.initial.schedule.weekdays,
      times: props.initial.schedule.times,
      period: props.initial.schedule.period,
    }
  : { kind: 'Interval', everyDays: 1 })
const withReminder = ref(props.initial ? props.initial.reminderAt !== null && props.initial.reminderAt !== undefined : true)
const reminderTime = ref(props.initial?.reminderAt?.slice(0, 5) ?? '19:00')

// One friend is the obvious checker — usually whoever sent the link. More
// than one is a choice the person makes.
const participantIds = ref<string[]>(props.friends.length === 1 ? [props.friends[0]!.person.id] : [])

function useIdea(template: GoalTemplate) {
  title.value = t.value.create.templates[template.key]
  category.value = template.category
  schedule.value = { ...template.schedule }
}

function toggleFriend(personId: string) {
  participantIds.value = participantIds.value.includes(personId)
    ? participantIds.value.filter(id => id !== personId)
    : [...participantIds.value, personId]
}

/*
 * Reported once mounted, not during setup. The submit button is the screen's
 * and is rendered before this form's setup runs in the browser, so an answer
 * given during setup arrived on the server in time and in the browser too
 * late — the two renders disagreed while hydrating.
 */
onMounted(() => {
  watchEffect(() => {
    valid.value = title.value.trim().length > 0
      && (!withReminder.value || /^\d{2}:\d{2}$/.test(reminderTime.value))
      && (props.friends.length === 0 || participantIds.value.length > 0)
  })
})

/** What is wrong with one field, worded in the reader's language from the server's code (#80). */
function fieldError(field: string): string | undefined {
  return fieldErrorText(props.error, field, t.value)
}

function onSubmit() {
  if (!valid.value) return

  emit('submit', {
    title: title.value.trim(),
    category: category.value,
    schedule: schedule.value,
    reminderAt: withReminder.value ? `${reminderTime.value}:00` : null,
    participantIds: props.friends.length > 0 ? [...participantIds.value] : [],
  })
}
</script>

<template>
  <form
    :id="formId"
    class="flex flex-col gap-5"
    novalidate
    data-testid="goal-draft-form"
    @submit.prevent="onSubmit"
  >
    <div class="flex flex-col gap-2.5">
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
          data-testid="draft-title-input"
        />
      </UFormField>

      <div v-if="title.trim().length === 0">
        <p class="mb-2 text-[13px] font-semibold text-(--ui-text-muted)">
          {{ t.onboarding.firstDo.ideas }}
        </p>
        <ul class="flex list-none flex-wrap gap-2 p-0">
          <li
            v-for="template in goalTemplates"
            :key="template.key"
          >
            <button
              type="button"
              class="q2-press inline-flex min-h-11 items-center gap-1.5 rounded-full border border-(--ui-border) bg-(--q2-surface) px-3 text-[13px] font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
              :data-testid="`draft-idea-${template.key}`"
              @click="useIdea(template)"
            >
              <UIcon
                :name="categoryIconName(template.category)"
                class="size-4 text-(--ui-text-muted)"
                aria-hidden="true"
              />
              {{ t.create.templates[template.key] }}
            </button>
          </li>
        </ul>
      </div>
    </div>

    <UFormField
      :label="t.onboarding.firstDo.rhythm"
      name="schedule"
      :error="fieldError('schedule')"
    >
      <SchedulePicker
        v-model="schedule"
        :kinds="kinds"
      />
    </UFormField>

    <div class="flex flex-col gap-3 rounded-(--q2-radius-lg) border border-(--ui-border) px-4 py-3">
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
          data-testid="draft-reminder-input"
        />
      </UFormField>
    </div>

    <UFormField
      v-if="friends.length > 0"
      :label="t.onboarding.firstDo.checkers"
      :help="t.onboarding.firstDo.checkersHint"
      name="participantIds"
      required
      :error="fieldError('participantIds')"
    >
      <ul
        class="flex list-none flex-col gap-1.5 p-0"
        data-testid="draft-checkers"
      >
        <li
          v-for="friend in friends"
          :key="friend.person.id"
        >
          <label
            class="flex min-h-11 w-full cursor-pointer items-center gap-3 rounded-(--q2-radius-md) px-2 py-2"
            :data-testid="`draft-checker-${friend.person.id}`"
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

    <p
      v-if="error && Object.keys(error.fieldErrors).length === 0"
      class="text-sm text-(--ui-error)"
      role="alert"
      data-testid="draft-form-error"
    >
      {{ t.errors[error.kind] }}
    </p>
  </form>
</template>
