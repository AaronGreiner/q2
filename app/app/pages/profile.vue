<script setup lang="ts">
import type { Image } from '~/api/types'

/**
 * Your own profile: who you are, how you are doing, and what you have earned.
 *
 * Shares the `profile` async-data key with the layout, so the badges in the tab
 * bar and this screen come from one request.
 */
const t = useMessages()
const now = useNow()

const { profile, error, isLoading, isSaving, refresh, rename, chooseAvatar, removeAvatar } = useProfile()

/*
 * Two sheets, and never both at once.
 *
 * Editing owns the name and the picture; taking the picture is its own sheet.
 * They are siblings here rather than nested because two open `UDrawer`s
 * deadlock — see `PhotoCapture` for what that looks like — and because a sheet
 * on top of a sheet does not fit on a phone.
 */
const editing = ref(false)
const capturing = ref(false)

useHead({ title: () => t.value.profile.heading })

async function onSave(value: { displayName: string }) {
  if (await rename(value.displayName)) editing.value = false
}

function onCapture() {
  editing.value = false
  capturing.value = true
}

/**
 * Brings the edit sheet back once the camera is gone, whether a picture was
 * taken or the sheet was simply dismissed. Watching the flag rather than
 * handling the two cases separately is what makes "swiped it away" behave like
 * "pressed cancel" without a third path to forget.
 */
watch(capturing, (open) => {
  if (!open) editing.value = true
})

async function onPhoto(image: Image) {
  capturing.value = false
  await chooseAvatar(image.id)
}
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader :title="t.profile.heading">
      <template #actions>
        <UButton
          icon="i-lucide-pencil"
          color="neutral"
          variant="outline"
          size="lg"
          :ui="{ base: 'size-11 justify-center rounded-full' }"
          :aria-label="t.profileEdit.open"
          :disabled="!profile"
          data-testid="open-profile-edit"
          @click="editing = true"
        />

        <UButton
          to="/settings"
          icon="i-lucide-settings"
          color="neutral"
          variant="outline"
          size="lg"
          :ui="{ base: 'size-11 justify-center rounded-full' }"
          :aria-label="t.profile.openSettings"
          data-testid="open-settings"
        />
      </template>
    </AppScreenHeader>

    <div class="q2-scroll flex-1 px-[18px] pt-1.5 pb-6">
      <div
        v-if="isLoading"
        class="flex flex-col items-center gap-4"
        aria-busy="true"
        aria-live="polite"
      >
        <span class="sr-only">{{ t.common.loading }}</span>
        <USkeleton class="size-22 rounded-full" />
        <USkeleton class="h-6 w-40" />
        <USkeleton class="h-20 w-full rounded-(--q2-radius-lg)" />
      </div>

      <AppErrorState
        v-else-if="error"
        :error="error"
        retryable
        @retry="refresh()"
      />

      <template v-else-if="profile">
        <section
          class="flex flex-col items-center py-1.5 text-center"
          aria-labelledby="profile-name"
          data-q2-private
        >
          <AppAvatar
            :initials="profile.person.initials"
            :color="profile.person.avatarColor"
            :image-id="profile.person.avatarImageId"
            :size="88"
          />

          <h2
            id="profile-name"
            class="mt-3 text-[21px] font-extrabold"
          >
            {{ profile.person.displayName }}
          </h2>
          <p class="text-[13px] font-semibold text-(--ui-text-muted)">
            {{ profile.person.handle }}
          </p>

          <p class="mt-2.5 flex items-center gap-1.5 rounded-full bg-(--q2-flame-soft) px-3 py-1.5 text-xs font-extrabold text-(--q2-flame-text)">
            <UIcon
              name="i-lucide-flame"
              class="size-3.5"
              aria-hidden="true"
            />
            {{ t.profile.streakBadge(profile.streak) }}
          </p>
        </section>

        <dl
          class="mt-4 flex list-none gap-2.5"
          data-q2-private
        >
          <div class="q2-card flex-1 px-2 py-3.5 text-center">
            <dd class="text-[22px] font-extrabold">
              {{ profile.streak }}
            </dd>
            <dt class="mt-0.5 text-[11px] font-bold text-(--ui-text-muted)">
              {{ t.profile.streak }}
            </dt>
          </div>
          <div class="q2-card flex-1 px-2 py-3.5 text-center">
            <dd class="text-[22px] font-extrabold">
              {{ profile.kudosReceived }}
            </dd>
            <dt class="mt-0.5 text-[11px] font-bold text-(--ui-text-muted)">
              {{ t.profile.kudos }}
            </dt>
          </div>
          <div class="q2-card flex-1 px-2 py-3.5 text-center">
            <dd class="text-[22px] font-extrabold">
              {{ profile.goalsCompleted }}
            </dd>
            <dt class="mt-0.5 text-[11px] font-bold text-(--ui-text-muted)">
              {{ t.profile.goals }}
            </dt>
          </div>
        </dl>

        <BalanceCard
          class="mt-3"
          :balance="profile.balance"
        />

        <section
          class="mt-6"
          aria-labelledby="badges-heading"
        >
          <h2
            id="badges-heading"
            class="mb-3 px-0.5 text-base font-extrabold"
          >
            {{ t.profile.badges }}
          </h2>

          <BadgeGrid :badges="profile.badges" />
        </section>

        <section
          class="mt-6"
          aria-labelledby="activity-heading"
        >
          <h2
            id="activity-heading"
            class="mb-3 px-0.5 text-base font-extrabold"
          >
            {{ t.profile.activity }}
          </h2>

          <div
            v-if="profile.recentActivity.length > 0"
            class="flex flex-col gap-2.5"
            data-testid="own-activity"
          >
            <ActivityRow
              v-for="entry in profile.recentActivity"
              :key="entry.id"
              :activity="entry"
              :now="now"
              readonly
            />
          </div>

          <AppStateMessage
            v-else
            icon="i-lucide-sparkles"
            :title="t.profile.noActivity"
            :description="t.profile.noActivityHint"
          />
        </section>
      </template>
    </div>

    <template v-if="profile">
      <ProfileEditSheet
        v-model:open="editing"
        :person="profile.person"
        :submitting="isSaving"
        @save="onSave"
        @capture="onCapture"
        @remove="profile.person.avatarImageId && removeAvatar(profile.person.avatarImageId)"
      />

      <PhotoCapture
        v-model:open="capturing"
        purpose="Avatar"
        @uploaded="onPhoto"
      />
    </template>
  </div>
</template>
