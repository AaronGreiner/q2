<script setup lang="ts">
import type { Person } from '~/api/types'

/**
 * The people this person has blocked.
 *
 * Only the ones they blocked — never the ones who blocked them. The second list
 * would be the announcement the whole feature is built to avoid, and it is not
 * information anybody is entitled to about somebody who walked away from them.
 *
 * The screen is deliberately dull. Nothing here is a decision to encourage or
 * discourage; it is a record with one button on it.
 */
definePageMeta({ layout: 'plain' })

const t = useMessages()

const { people, error, isLoading, isUnblocking, refresh, unblock } = useBlockedPeople()

/**
 * Two refs rather than one derived from the other: the dialog closes itself
 * *before* it emits, so a value cleared by closing would be null by the time
 * the answer arrived.
 */
const releasing = ref<Person | null>(null)
const isConfirming = ref(false)

function askToUnblock(person: Person) {
  releasing.value = person
  isConfirming.value = true
}

async function onUnblock() {
  const person = releasing.value
  releasing.value = null

  if (person) await unblock(person.id)
}

useHead({ title: () => t.value.safety.blockedHeading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader
      :title="t.safety.blockedHeading"
      :eyebrow="t.safety.blockedSubtitle"
      back-to="/settings"
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

      <ul
        v-else-if="people.length > 0"
        class="flex list-none flex-col gap-2.5 p-0"
        data-testid="blocked-list"
      >
        <li
          v-for="person in people"
          :key="person.id"
          class="q2-card flex items-center gap-3 px-3 py-3"
          data-testid="blocked-row"
        >
          <AppAvatar
            :initials="person.initials"
            :color="person.avatarColor"
            :image-id="person.avatarImageId"
            :size="40"
          />

          <div class="min-w-0 flex-1">
            <p
              class="truncate text-sm font-bold"
              data-q2-private
            >
              {{ person.displayName }}
            </p>
            <p
              class="mt-0.5 truncate text-[11px] font-semibold text-(--ui-text-muted)"
              data-q2-private
            >
              {{ person.handle }}
            </p>
          </div>

          <UButton
            class="min-h-11 justify-center"
            size="sm"
            color="neutral"
            variant="outline"
            :label="t.safety.unblock"
            :disabled="isUnblocking"
            data-testid="unblock"
            @click="askToUnblock(person)"
          />
        </li>
      </ul>

      <AppStateMessage
        v-else
        icon="i-lucide-shield"
        :title="t.safety.blockedEmpty"
        :description="t.safety.blockedEmptyHint"
        data-testid="blocked-empty"
      />
    </div>

    <AppConfirmDialog
      v-model:open="isConfirming"
      :title="t.safety.unblockHeading"
      :description="t.safety.unblockBody"
      :confirm-label="t.safety.unblock"
      @confirm="onUnblock"
    />
  </div>
</template>
