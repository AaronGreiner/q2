<script setup lang="ts">
import { imageUrl } from '~/api/images'
import type { OwnProof } from '~/api/types'

/**
 * One of your photographs, as large as the phone allows.
 *
 * A modal rather than a page: it is a closer look at something already on
 * screen, and closing it has to put you back exactly where you were in the
 * grid. Full screen, because a photograph shrunk into a dialog is not a closer
 * look.
 *
 * The goal and the day come with it, and a way to the goal — the vote, the
 * windows and the conversation all live there, and none of them are repeated
 * here.
 */
const props = defineProps<{ proof: OwnProof | null }>()

const open = defineModel<boolean>('open', { required: true })

const t = useMessages()
const { public: config } = useRuntimeConfig()

const failed = ref(false)

watch(() => props.proof?.imageId, () => {
  failed.value = false
})

const source = computed(() =>
  (!props.proof || failed.value ? null : imageUrl(config.apiBaseUrl, props.proof.imageId)))

const description = computed(() => (props.proof
  ? `${t.value.proofGallery.deliveredOn(formatInstantDate(props.proof.createdAt))} · ${proofStatusLabel(props.proof.status, t.value)}`
  : ''))
</script>

<template>
  <UModal
    v-model:open="open"
    fullscreen
    :title="proof?.goalTitle ?? ''"
    :description="description"
    :ui="{
      content: 'bg-(--ui-bg) pt-[env(safe-area-inset-top)] pb-(--q2-safe-bottom) divide-y-0',
      header: 'pe-14',
      body: 'flex min-h-0 items-center justify-center p-0 sm:p-0',
    }"
  >
    <template #title>
      <span
        v-if="proof"
        class="flex min-w-0 items-center gap-2 font-extrabold"
        data-q2-private
      >
        <UIcon
          :name="goalIconName(proof.goalIcon)"
          class="size-4 shrink-0"
          aria-hidden="true"
        />
        <span class="truncate">{{ proof.goalTitle }}</span>
      </span>
    </template>

    <!-- Our own button in the slot, because Nuxt UI's would be labelled from
         its own locale rather than from the app's catalogue. -->
    <template #close="{ ui }">
      <UButton
        icon="i-lucide-x"
        color="neutral"
        variant="ghost"
        size="lg"
        :aria-label="t.common.close"
        :class="ui.close({ class: 'top-[calc(0.75rem+env(safe-area-inset-top))] size-11 justify-center rounded-full' })"
        data-testid="proof-viewer-close"
      />
    </template>

    <template #body>
      <div
        class="flex size-full items-center justify-center"
        data-testid="proof-viewer"
        data-q2-block
      >
        <img
          v-if="source"
          :src="source"
          :alt="proof?.goalTitle ?? ''"
          crossorigin="use-credentials"
          decoding="async"
          class="size-full object-contain"
          @error="failed = true"
        >

        <AppStateMessage
          v-else
          icon="i-lucide-image-off"
          :title="t.proofGallery.unavailable"
        />
      </div>
    </template>

    <template #footer>
      <UButton
        v-if="proof"
        :to="`/goals/${proof.goalId}`"
        color="neutral"
        variant="outline"
        size="lg"
        block
        trailing-icon="i-lucide-arrow-right"
        class="min-h-11 justify-center font-bold"
        data-testid="proof-viewer-goal"
      >
        {{ t.proofGallery.openGoal }}
      </UButton>
    </template>
  </UModal>
</template>
