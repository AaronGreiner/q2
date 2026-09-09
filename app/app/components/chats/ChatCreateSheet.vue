<script setup lang="ts">
import { groupIcons, type Friend } from '~/api/types'

/**
 * Starting a conversation: with one friend, or with several.
 *
 * Both live in one sheet because they are the same decision — "who am I
 * writing to?" — and splitting them across two entry points would mean
 * choosing before you know which one you want.
 *
 * Only friends appear. That is the server's rule too, and offering somebody a
 * name it would then refuse would be worse than not offering it.
 */
const props = defineProps<{
  friends: Friend[]
  submitting: boolean
}>()

const emit = defineEmits<{
  direct: [personId: string]
  group: [value: { title: string, icon: string, memberIds: string[] }]
}>()

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()

const mode = ref<'direct' | 'group'>('direct')
const title = ref('')
const icon = ref<string>(groupIcons[0]!)
const selected = ref<string[]>([])

const canCreateGroup = computed(() =>
  title.value.trim().length > 0 && selected.value.length > 0 && !props.submitting)

function toggleMember(personId: string) {
  selected.value = selected.value.includes(personId)
    ? selected.value.filter(id => id !== personId)
    : [...selected.value, personId]
}

function onCreateGroup() {
  if (!canCreateGroup.value) return

  emit('group', {
    title: title.value.trim(),
    icon: icon.value,
    memberIds: [...selected.value],
  })
}

/** Called by the page once a conversation has actually been created. */
function reset() {
  mode.value = 'direct'
  title.value = ''
  icon.value = groupIcons[0]!
  selected.value = []
}

defineExpose({ reset })
</script>

<template>
  <UDrawer
    v-model:open="open"
    :title="t.chats.newChatHeading"
    :description="t.chats.newChatSubtitle"
    :ui="{ container: 'max-w-[430px] mx-auto' }"
  >
    <template #body>
      <div class="flex flex-col gap-4 pb-2">
        <div
          class="flex gap-2"
          role="radiogroup"
          :aria-label="t.chats.newChatHeading"
        >
          <button
            v-for="option in (['direct', 'group'] as const)"
            :key="option"
            type="button"
            role="radio"
            :aria-checked="mode === option"
            class="min-h-11 flex-1 rounded-full border px-3.5 text-[13px] font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
            :class="mode === option
              ? 'border-transparent bg-(--q2-accent-solid) text-(--q2-accent-contrast)'
              : 'border-(--ui-border) bg-(--q2-surface) text-(--ui-text-muted)'"
            :data-testid="`chat-mode-${option}`"
            @click="mode = option"
          >
            {{ option === 'direct' ? t.chats.tabDirect : t.chats.tabGroup }}
          </button>
        </div>

        <AppStateMessage
          v-if="friends.length === 0"
          icon="i-lucide-users"
          :title="t.chats.noFriends"
          :description="t.chats.noFriendsHint"
          data-testid="chat-create-no-friends"
        />

        <template v-else-if="mode === 'direct'">
          <h3 class="px-0.5 text-sm font-extrabold">
            {{ t.chats.pickFriend }}
          </h3>

          <ul
            class="flex max-h-[46vh] list-none flex-col gap-1.5 overflow-y-auto p-0"
            data-testid="chat-friend-picker"
          >
            <li
              v-for="friend in friends"
              :key="friend.person.id"
            >
              <button
                type="button"
                class="flex min-h-11 w-full items-center gap-3 rounded-(--q2-radius-md) px-2 py-2 text-start focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
                :disabled="submitting"
                :data-testid="`chat-with-${friend.person.id}`"
                @click="emit('direct', friend.person.id)"
              >
                <AppAvatar
                  :initials="friend.person.initials"
                  :color="friend.person.avatarColor"
                  :image-id="friend.person.avatarImageId"
                  :size="38"
                  :online="friend.person.isOnline"
                />

                <span
                  class="min-w-0 flex-1"
                  data-q2-private
                >
                  <span class="block truncate text-sm font-bold">{{ friend.person.displayName }}</span>
                  <span class="mt-0.5 block truncate text-[11px] font-semibold text-(--ui-text-muted)">
                    {{ friend.person.handle }}
                  </span>
                </span>

                <UIcon
                  name="i-lucide-chevron-right"
                  class="size-5 shrink-0 text-(--ui-text-dimmed)"
                  aria-hidden="true"
                />
              </button>
            </li>
          </ul>
        </template>

        <form
          v-else
          class="flex flex-col gap-4"
          novalidate
          data-testid="group-create-form"
          @submit.prevent="onCreateGroup"
        >
          <UFormField
            :label="t.chats.groupName"
            name="title"
            required
          >
            <UInput
              v-model="title"
              :placeholder="t.chats.groupNamePlaceholder"
              :maxlength="80"
              autocomplete="off"
              size="xl"
              class="w-full"
              data-testid="group-title-input"
            />
          </UFormField>

          <UFormField
            :label="t.chats.groupIcon"
            name="icon"
          >
            <div
              class="flex flex-wrap gap-2"
              role="radiogroup"
              :aria-label="t.chats.groupIcon"
            >
              <button
                v-for="option in groupIcons"
                :key="option"
                type="button"
                role="radio"
                :aria-checked="icon === option"
                :aria-label="option"
                class="flex size-11 items-center justify-center rounded-(--q2-radius-md) border focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--ui-primary)"
                :class="icon === option
                  ? 'border-transparent bg-(--q2-accent-solid) text-(--q2-accent-contrast)'
                  : 'border-(--ui-border) bg-(--q2-surface) text-(--ui-text-muted)'"
                :data-testid="`group-icon-${option}`"
                @click="icon = option"
              >
                <UIcon
                  :name="groupIconName(option)"
                  class="size-5"
                  aria-hidden="true"
                />
              </button>
            </div>
          </UFormField>

          <UFormField
            :label="t.chats.groupMembers"
            name="members"
            :help="t.chats.groupMembersHint"
          >
            <ul class="flex max-h-[32vh] list-none flex-col gap-1.5 overflow-y-auto p-0">
              <li
                v-for="friend in friends"
                :key="friend.person.id"
              >
                <label
                  class="flex min-h-11 w-full cursor-pointer items-center gap-3 rounded-(--q2-radius-md) px-2 py-2"
                  :data-testid="`group-member-${friend.person.id}`"
                >
                  <UCheckbox
                    :model-value="selected.includes(friend.person.id)"
                    @update:model-value="toggleMember(friend.person.id)"
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

          <UButton
            type="submit"
            block
            size="xl"
            :disabled="!canCreateGroup"
            :loading="submitting"
            class="min-h-11 justify-center font-extrabold"
            data-testid="group-create-submit"
          >
            {{ t.chats.createGroup }}
          </UButton>
        </form>
      </div>
    </template>
  </UDrawer>
</template>
