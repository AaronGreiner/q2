<script setup lang="ts">
import type { ReportReason } from '~/api/types'

/**
 * Somebody else's profile.
 *
 * The screen the balance exists for, and the one place q2 shows one person's
 * record to another. What it shows is scoped by the server to the goals the two
 * of them share — this page never filters anything, and that is deliberate: a
 * screen that could filter is a screen that could stop.
 *
 * Loaded on the server so a shared link has the right title.
 */
definePageMeta({ layout: 'plain' })

const route = useRoute()
const t = useMessages()

const id = computed(() => String(route.params.id))
const { person, error, isMissing, isLoading, refresh } = usePersonProfile(id)

const { isBusy, report, block } = useSafety()

const isReporting = ref(false)
const isConfirmingBlock = ref(false)

async function onReport(reason: ReportReason, note: string) {
  if (await report('Person', id.value, reason, note)) isReporting.value = false
}

/*
 * The sheet closes before the question opens.
 *
 * Two drawers open at once deadlock — vaul drives both from one set of body
 * styles — so the report sheet gets out of the way first and the confirmation
 * follows, which is the same dance PhotoCapture documents.
 */
function askToBlock() {
  isReporting.value = false
  isConfirmingBlock.value = true
}

async function onBlock() {
  // Straight out of the screen: a blocked person has no profile any more, so
  // staying here would leave somebody looking at a page that is now a 404.
  if (await block(id.value)) await navigateTo('/search')
}

useHead({ title: () => person.value?.person.displayName ?? t.value.person.heading })
</script>

<template>
  <div class="flex min-h-0 flex-1 flex-col">
    <AppScreenHeader
      :title="t.person.heading"
      back-to="/search"
    >
      <!--
        Behind one control rather than two buttons on the header: reporting and
        blocking are not things to offer at the same weight as looking at
        somebody's streak. It appears only once there is a person to act on.
      -->
      <template
        v-if="person"
        #actions
      >
        <UButton
          icon="i-lucide-ellipsis"
          color="neutral"
          variant="outline"
          size="lg"
          :ui="{ base: 'size-11 justify-center rounded-full' }"
          :aria-label="t.safety.report"
          data-testid="person-actions"
          @click="isReporting = true"
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

      <AppStateMessage
        v-else-if="isMissing"
        icon="i-lucide-user"
        :title="t.person.notFound"
        :description="t.person.notFoundHint"
        data-testid="person-missing"
      />

      <AppErrorState
        v-else-if="error"
        :error="error"
        retryable
        @retry="refresh()"
      />

      <template v-else-if="person">
        <section
          class="flex flex-col items-center py-1.5 text-center"
          aria-labelledby="person-name"
          data-q2-private
        >
          <AppAvatar
            :initials="person.person.initials"
            :color="person.person.avatarColor"
            :image-id="person.person.avatarImageId"
            :size="88"
          />

          <h2
            id="person-name"
            class="mt-3 text-[21px] font-extrabold"
          >
            {{ person.person.displayName }}
          </h2>
          <p class="text-[13px] font-semibold text-(--ui-text-muted)">
            {{ person.person.handle }}
          </p>

          <p
            v-if="person.streak > 0"
            class="mt-2.5 flex items-center gap-1.5 rounded-full bg-(--q2-flame-soft) px-3 py-1.5 text-xs font-extrabold text-(--q2-flame-text)"
          >
            <UIcon
              name="i-lucide-flame"
              class="size-3.5"
              aria-hidden="true"
            />
            {{ t.profile.streakBadge(person.streak) }}
          </p>
        </section>

        <dl
          class="mt-4 flex list-none gap-2.5"
          data-q2-private
        >
          <div class="q2-card flex-1 px-2 py-3.5 text-center">
            <dd class="text-[22px] font-extrabold">
              {{ person.streak }}
            </dd>
            <dt class="mt-0.5 text-[11px] font-bold text-(--ui-text-muted)">
              {{ t.person.streak }}
            </dt>
          </div>
          <div class="q2-card flex-1 px-2 py-3.5 text-center">
            <dd class="text-[22px] font-extrabold">
              {{ person.kudosReceived }}
            </dd>
            <dt class="mt-0.5 text-[11px] font-bold text-(--ui-text-muted)">
              {{ t.person.kudos }}
            </dt>
          </div>
          <div class="q2-card flex-1 px-2 py-3.5 text-center">
            <dd class="text-[22px] font-extrabold">
              {{ person.goalsCompleted }}
            </dd>
            <dt class="mt-0.5 text-[11px] font-bold text-(--ui-text-muted)">
              {{ t.person.completed }}
            </dt>
          </div>
        </dl>

        <!--
          The record, and the only thing on this screen that is scoped. It says
          how many to-dos it covers, because "0 · 0" means two very different
          things depending on whether they share anything at all.
        -->
        <BalanceCard
          class="mt-3"
          :balance="person.balance"
          :shared-goals="person.sharedGoals"
        />
      </template>
    </div>

    <ReportSheet
      v-model:open="isReporting"
      target-kind="Person"
      :target-id="id"
      :person-id="id"
      :busy="isBusy"
      @report="onReport"
      @block="askToBlock"
    />

    <AppConfirmDialog
      v-model:open="isConfirmingBlock"
      :title="t.safety.blockHeading"
      :description="t.safety.blockBody"
      :confirm-label="t.safety.blockConfirm"
      @confirm="onBlock"
    />
  </div>
</template>
