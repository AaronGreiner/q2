<script setup lang="ts">
import * as Sentry from '@sentry/nuxt'
import type { NuxtError } from '#app'

/**
 * The last line of defence: anything not handled inside a page ends up here —
 * an unhandled render error, a failed route, a Nitro server error.
 *
 * Two responsibilities:
 *  - show a human something useful instead of a blank screen;
 *  - make sure the incident is visible in Sentry exactly once.
 *
 * The Nuxt Sentry integration already captures unhandled errors, so this page
 * only adds context. Capturing again here would create a duplicate issue for
 * one failure.
 */
const props = defineProps<{ error: NuxtError }>()

const t = useMessages()
const feedback = useFeedback()
const isNotFound = computed(() => props.error.statusCode === 404)

/*
 * The one screen where somebody knows something Sentry does not: what they were
 * doing when it broke. Not offered for a 404, which is a mistyped URL rather
 * than a defect, and not offered without a DSN, where there is nothing to open.
 * The message is tagged `error-page` and carries the session replay with it, so
 * the words land next to the recording of what produced them.
 */
const canGiveFeedback = computed(() => !isNotFound.value && feedback.isAvailable.value)

onMounted(() => {
  if (isNotFound.value) {
    // A mistyped URL is not a defect.
    return
  }

  Sentry.addBreadcrumb({
    category: 'q2.error-page',
    level: 'error',
    message: `Error page shown for status ${props.error.statusCode}`,
  })
})

async function goHome() {
  // Clears the error state before navigating, otherwise the page stays.
  await clearError({ redirect: '/' })
}

useHead({ title: () => (isNotFound.value ? t.value.errors.pageNotFound : t.value.errors.title.other) })
</script>

<template>
  <div class="mx-auto flex h-dvh w-full max-w-[430px] items-center justify-center bg-(--ui-bg) px-6">
    <div
      class="flex flex-col items-center gap-4 text-center"
      role="alert"
      data-testid="app-error"
    >
      <UIcon
        :name="isNotFound ? 'i-lucide-compass' : 'i-lucide-triangle-alert'"
        class="size-10 text-(--ui-text-dimmed)"
        aria-hidden="true"
      />

      <h1 class="text-xl font-extrabold text-(--ui-text)">
        {{ isNotFound ? t.errors.pageNotFound : t.errors.title.other }}
      </h1>

      <!--
        Never `error.message` or `error.stack`: those are written for
        developers and can contain internals.
      -->
      <p class="text-(--ui-text-muted)">
        {{ isNotFound ? t.errors.pageNotFoundHint : t.errors.unexpected }}
      </p>

      <div class="flex flex-col items-center gap-2">
        <UButton
          icon="i-lucide-house"
          data-testid="app-error-home"
          @click="goHome"
        >
          {{ t.common.toHome }}
        </UButton>

        <UButton
          v-if="canGiveFeedback"
          variant="ghost"
          color="neutral"
          icon="i-lucide-message-square-heart"
          class="min-h-11"
          :loading="feedback.isOpening.value"
          data-testid="app-error-feedback"
          @click="feedback.open('error-page')"
        >
          {{ t.feedback.fromErrorPage }}
        </UButton>
      </div>
    </div>
  </div>
</template>
