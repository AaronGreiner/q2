<script setup lang="ts">
import type { Image, OwnProof } from '~/api/types'

/**
 * Your own profile: who you are, how you are doing, and what you have earned.
 *
 * Shares the `profile` async-data key with the layout, so the badges in the tab
 * bar and this screen come from one request.
 *
 * Your photographs are a read of their own rather than part of the profile,
 * because the profile is also the start screen's header and has no business
 * carrying a gallery there.
 */
const t = useMessages()
const now = useNow()

const { profile, error, isLoading, isSaving, refresh, rename, chooseAvatar, removeAvatar } = useProfile()
const { proofs, error: proofsError, isLoading: proofsLoading, refresh: refreshProofs } = useOwnProofs()

/** One row and a half of the grid: enough to recognise, not enough to scroll past. */
const previewSize = 6
const preview = computed(() => proofs.value.slice(0, previewSize))

const viewing = ref(false)
const viewed = ref<OwnProof | null>(null)

function view(proof: OwnProof) {
  viewed.value = proof
  viewing.value = true
}

/*
 * A sheet and a camera, and never both at once.
 *
 * Editing owns the name and the picture; taking the picture is its own
 * full-screen camera. They are siblings here rather than nested: overlays
 * stacked on overlays have deadlocked in this app before — see `PhotoCapture`
 * — and a camera over a half-hidden form does not fit on a phone.
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

    <AppContentPanel>
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
            :expand-title="profile.person.displayName"
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
          aria-labelledby="proofs-heading"
        >
          <h2
            id="proofs-heading"
            class="mb-3 px-0.5 text-base font-extrabold"
          >
            {{ t.proofGallery.heading }}
          </h2>

          <div
            v-if="proofsLoading"
            class="grid grid-cols-3 gap-1.5"
            aria-busy="true"
            aria-live="polite"
          >
            <span class="sr-only">{{ t.common.loading }}</span>
            <USkeleton
              v-for="tile in 3"
              :key="tile"
              class="aspect-square w-full rounded-(--q2-radius-md)"
            />
          </div>

          <AppErrorState
            v-else-if="proofsError"
            :error="proofsError"
            retryable
            @retry="refreshProofs()"
          />

          <template v-else-if="preview.length > 0">
            <div
              class="grid grid-cols-3 gap-1.5"
              data-testid="own-proofs"
            >
              <ProofGalleryTile
                v-for="proof in preview"
                :key="proof.id"
                :proof="proof"
                @open="view(proof)"
              />
            </div>

            <UButton
              to="/profile/photos"
              class="mt-2 min-h-11 w-full justify-center"
              size="lg"
              color="neutral"
              variant="ghost"
              icon="i-lucide-images"
              :label="`${t.proofGallery.showAll} · ${t.proofGallery.count(proofs.length)}`"
              data-testid="open-proof-gallery"
            />
          </template>

          <AppStateMessage
            v-else
            icon="i-lucide-camera"
            :title="t.proofGallery.empty"
            :description="t.proofGallery.emptyHint"
            data-testid="own-proofs-empty"
          />
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
    </AppContentPanel>

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

    <ProofPhotoViewer
      v-model:open="viewing"
      :proof="viewed"
    />
  </div>
</template>
