<script setup lang="ts">
import type { Person } from '~/api/types'

/**
 * Who checks a goal: a list of people to tick, with a search of its own once
 * it is longer than a screen. Used where a goal is made — from the owner's
 * friends — and where it is edited, where the people already on it are listed
 * too, friends or not (docs/adr/0044-editing-a-running-goal.md).
 */
const props = defineProps<{
  people: readonly Person[]
}>()

/** The chosen people's ids. */
const model = defineModel<string[]>({ required: true })

const t = useMessages()

const query = ref('')

/** Only worth a search field once the list is longer than a screen. */
const showSearch = computed(() => props.people.length > 6)

const shown = computed(() => {
  const wanted = query.value.trim().toLowerCase()
  if (!wanted) return props.people

  return props.people.filter(person =>
    person.displayName.toLowerCase().includes(wanted)
    || person.handle.toLowerCase().includes(wanted))
})

function toggle(personId: string) {
  model.value = model.value.includes(personId)
    ? model.value.filter(id => id !== personId)
    : [...model.value, personId]
}
</script>

<template>
  <div>
    <UInput
      v-if="showSearch"
      v-model="query"
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
        v-for="person in shown"
        :key="person.id"
      >
        <label
          class="flex min-h-11 w-full cursor-pointer items-center gap-3 rounded-(--q2-radius-md) px-2 py-2"
          :data-testid="`goal-friend-${person.id}`"
        >
          <UCheckbox
            :model-value="model.includes(person.id)"
            @update:model-value="toggle(person.id)"
          />

          <AppAvatar
            :initials="person.initials"
            :color="person.avatarColor"
            :image-id="person.avatarImageId"
            :size="32"
          />

          <span
            class="min-w-0 flex-1 truncate text-sm font-bold"
            data-q2-private
          >
            {{ person.displayName }}
          </span>
        </label>
      </li>

      <li
        v-if="shown.length === 0"
        class="px-2 py-3 text-sm font-semibold text-(--ui-text-muted)"
        role="status"
      >
        {{ t.create.friendsNoMatch }}
      </li>
    </ul>
  </div>
</template>
